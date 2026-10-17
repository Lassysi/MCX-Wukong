package dev.rehan.passthrough.client;

import com.sun.jna.Library;
import com.sun.jna.Native;
import com.sun.jna.Pointer;
import com.sun.jna.WString;
import com.sun.jna.win32.StdCallLibrary;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.mixin.GameRendererAccess;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import net.minecraft.client.Minecraft;
import org.lwjgl.opengl.GL11;
import org.lwjgl.opengl.GL15;
import org.lwjgl.opengl.GL21;
import org.lwjgl.opengl.GL30;
import org.lwjgl.opengl.GL32;
import org.lwjgl.BufferUtils;
import com.mojang.blaze3d.systems.RenderSystem;

/** Same MCPT transport as the reference; captures vanilla/modded rendering on 1.21.4. */
public final class FrameExporter {
    private static final int HEADER=4096,SLOTS=8,DESC=256,DESC_BYTES=128,MAX_W=1920,MAX_H=1080;
    private static final long STRIDE=(long)MAX_W*MAX_H*12,TOTAL=HEADER+STRIDE*SLOTS;
    private static final Capture[] ring=new Capture[3];
    private static Pointer mapping,view;
    private static ByteBuffer header;
    private static boolean failed;
    private static int next,slot;
    private static long frame,published,lastPublishedFrame;
    private static Capture current;
    public static boolean capturingOverlay(){return current!=null;}
    public static boolean handPass;
    private static final ByteBuffer colorMask=BufferUtils.createByteBuffer(4);
    private static boolean handR,handG,handB,handA;
    private static boolean handLogged;
    public static void beginHand() {
        handPass=current!=null;
        if(!handPass)return;
        GL11.glGetBooleanv(GL11.GL_COLOR_WRITEMASK,colorMask);
        handR=colorMask.get(0)!=0;handG=colorMask.get(1)!=0;handB=colorMask.get(2)!=0;handA=colorMask.get(3)!=0;
        RenderSystem.colorMask(handR,handG,handB,true);
        if(!handLogged){
            handLogged=true;
            GL11.glGetBooleanv(GL11.GL_COLOR_WRITEMASK,colorMask);
            Passthrough.LOG.info("DQJ hand capture mask requested rgba={},{},{},true; GL={},{},{},{}; blendAlpha={},{}",
                handR,handG,handB,colorMask.get(0),colorMask.get(1),colorMask.get(2),colorMask.get(3),
                GL11.glGetInteger(org.lwjgl.opengl.GL14.GL_BLEND_SRC_ALPHA),GL11.glGetInteger(org.lwjgl.opengl.GL14.GL_BLEND_DST_ALPHA));
        }
    }
    public static void endHand() {
        if(!handPass)return;
        handPass=false;RenderSystem.colorMask(handR,handG,handB,handA);
    }
    private interface Kernel extends StdCallLibrary {
        Pointer CreateFileMappingW(Pointer file,Pointer security,int protection,int high,int low,WString name);
        Pointer MapViewOfFile(Pointer mapping,int access,int high,int low,long bytes);
        boolean UnmapViewOfFile(Pointer view);
        boolean CloseHandle(Pointer handle);
    }
    private static Kernel kernel;
    private static final class Capture {
        int pbo,width,height;long fence,number,captured;HostState.Pose pose;float far;
    }
    private FrameExporter() {}
    private static boolean ensure() {
        if(header!=null)return true;
        if(failed)return false;
        try {
            kernel=Native.load("kernel32",Kernel.class);
            mapping=kernel.CreateFileMappingW(new Pointer(-1),null,4,0,(int)TOTAL,new WString("Local\\MCPassthroughFrame"));
            if(mapping==null)throw new IllegalStateException("CreateFileMappingW failed");
            view=kernel.MapViewOfFile(mapping,0x000f001f,0,0,TOTAL);
            if(view==null)throw new IllegalStateException("MapViewOfFile failed");
            header=view.getByteBuffer(0,HEADER).order(ByteOrder.LITTLE_ENDIAN);
            header.putInt(0,0x5450434d);header.putInt(4,1);header.putInt(8,HEADER);header.putInt(12,SLOTS);
            header.putLong(16,STRIDE);header.putInt(24,MAX_W);header.putInt(28,MAX_H);
            header.putLong(32,0);header.putInt(40,-1);header.putInt(44,(int)ProcessHandle.current().pid());
            Passthrough.LOG.info("DQJ MCPT export ready: 1.21.4 conventional OpenGL depth");
            return true;
        }catch(Throwable e){failed=true;Passthrough.LOG.error("frame export failed",e);return false;}
    }
    private static void readPixels(int pbo,long offset,int format,int type,int width,int height) {
        int old=GL11.glGetInteger(GL21.GL_PIXEL_PACK_BUFFER_BINDING);
        int alignment=GL11.glGetInteger(GL11.GL_PACK_ALIGNMENT);
        try {GL15.glBindBuffer(GL21.GL_PIXEL_PACK_BUFFER,pbo);GL11.glPixelStorei(GL11.GL_PACK_ALIGNMENT,1);
            GL11.glReadPixels(0,0,width,height,format,type,offset);
        }finally{GL11.glPixelStorei(GL11.GL_PACK_ALIGNMENT,alignment);GL15.glBindBuffer(GL21.GL_PIXEL_PACK_BUFFER,old);}
    }
    public static void world() {
        current=null;
        if(!Passthrough.active||!ensure())return;
        finish();
        var mc=Minecraft.getInstance();var target=mc.getMainRenderTarget();
        int w=target.width,h=target.height;
        if(w<1||h<1||w>MAX_W||h>MAX_H)return;
        Capture c=ring[next];if(c==null)c=ring[next]=new Capture();
        if(c.fence!=0)return;
        long bytes=(long)w*h*12;
        int oldPack=GL11.glGetInteger(GL21.GL_PIXEL_PACK_BUFFER_BINDING);
        if(c.pbo==0||c.width!=w||c.height!=h){
            if(c.pbo!=0)GL15.glDeleteBuffers(c.pbo);c.pbo=GL15.glGenBuffers();
            GL15.glBindBuffer(GL21.GL_PIXEL_PACK_BUFFER,c.pbo);GL15.glBufferData(GL21.GL_PIXEL_PACK_BUFFER,bytes,GL15.GL_STREAM_READ);
            GL15.glBindBuffer(GL21.GL_PIXEL_PACK_BUFFER,oldPack);c.width=w;c.height=h;
        }
        var camera=mc.gameRenderer.getMainCamera();var at=camera.getPosition();var player=mc.player;
        float partial=mc.getDeltaTracker().getGameTimeDeltaPartialTick(true);
        float fov=((GameRendererAccess)mc.gameRenderer).dqj$getFov(camera,partial,true);
        c.pose=HostState.rendered(at.x,at.y,at.z,camera.getYRot(),camera.getXRot(),fov,
            mc.options.getCameraType().isFirstPerson(),player.getX(),player.getY(),player.getZ());
        c.far=mc.gameRenderer.getDepthFar();c.number=++frame;c.captured=System.nanoTime();
        NativeControl.publish(c.pose);
        int oldRead=GL11.glGetInteger(GL30.GL_READ_FRAMEBUFFER_BINDING);
        int oldDraw=GL11.glGetInteger(GL30.GL_DRAW_FRAMEBUFFER_BINDING);
        float[] clear=new float[4];GL11.glGetFloatv(GL11.GL_COLOR_CLEAR_VALUE,clear);
        boolean scissor=GL11.glIsEnabled(GL11.GL_SCISSOR_TEST);
        GL11.glGetBooleanv(GL11.GL_COLOR_WRITEMASK,colorMask);
        boolean maskR=colorMask.get(0)!=0,maskG=colorMask.get(1)!=0,maskB=colorMask.get(2)!=0,maskA=colorMask.get(3)!=0;
        try {
            GL30.glBindFramebuffer(GL30.GL_READ_FRAMEBUFFER,target.frameBufferId);
            readPixels(c.pbo,0,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,w,h);
            readPixels(c.pbo,(long)w*h*4,GL11.GL_DEPTH_COMPONENT,GL11.GL_FLOAT,w,h);
            GL30.glBindFramebuffer(GL30.GL_DRAW_FRAMEBUFFER,target.frameBufferId);
            GL11.glDisable(GL11.GL_SCISSOR_TEST);GL11.glColorMask(true,true,true,true);
            GL11.glClearColor(0,0,0,0);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);
        }finally{
            GL11.glClearColor(clear[0],clear[1],clear[2],clear[3]);if(scissor)GL11.glEnable(GL11.GL_SCISSOR_TEST);
            GL11.glColorMask(maskR,maskG,maskB,maskA);
            GL30.glBindFramebuffer(GL30.GL_READ_FRAMEBUFFER,oldRead);GL30.glBindFramebuffer(GL30.GL_DRAW_FRAMEBUFFER,oldDraw);
        }
        current=c;next=(next+1)%ring.length;
    }
    public static void overlay() {
        Capture c=current;current=null;if(c==null)return;
        var target=Minecraft.getInstance().getMainRenderTarget();
        if(target.width!=c.width||target.height!=c.height)return;
        int oldRead=GL11.glGetInteger(GL30.GL_READ_FRAMEBUFFER_BINDING);
        try {GL30.glBindFramebuffer(GL30.GL_READ_FRAMEBUFFER,target.frameBufferId);
            readPixels(c.pbo,(long)c.width*c.height*8,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,c.width,c.height);
            c.fence=GL32.glFenceSync(GL32.GL_SYNC_GPU_COMMANDS_COMPLETE,0);
        }finally{GL30.glBindFramebuffer(GL30.GL_READ_FRAMEBUFFER,oldRead);}
        finish();
    }
    private static void finish() {
        for(var c:ring){
            if(c==null||c.fence==0)continue;
            int status=GL32.glClientWaitSync(c.fence,0,0);
            if(status!=GL32.GL_ALREADY_SIGNALED&&status!=GL32.GL_CONDITION_SATISFIED)continue;
            if(c.number<=lastPublishedFrame){GL32.glDeleteSync(c.fence);c.fence=0;continue;}
            long bytes=(long)c.width*c.height*12;int d=DESC+slot*DESC_BYTES;
            long seq=header.getLong(d);header.putLong(d,(seq+1)|1);java.lang.invoke.VarHandle.fullFence();
            int oldPack=GL11.glGetInteger(GL21.GL_PIXEL_PACK_BUFFER_BINDING);
            try {
                GL15.glBindBuffer(GL21.GL_PIXEL_PACK_BUFFER,c.pbo);
                ByteBuffer buffer=GL30.glMapBufferRange(GL21.GL_PIXEL_PACK_BUFFER,0,bytes,GL30.GL_MAP_READ_BIT);
                if(buffer==null)throw new IllegalStateException("PBO read mapping failed");
                view.getByteBuffer(HEADER+slot*STRIDE,bytes).put(buffer);
                GL15.glUnmapBuffer(GL21.GL_PIXEL_PACK_BUFFER);
            }finally{GL15.glBindBuffer(GL21.GL_PIXEL_PACK_BUFFER,oldPack);}
            var p=c.pose;header.putLong(d+8,c.number);header.putLong(d+16,p.hostFrame());
            header.putInt(d+24,c.width);header.putInt(d+28,c.height);header.putFloat(d+32,.05f);header.putFloat(d+36,c.far);
            header.putFloat(d+40,p.fov());header.putInt(d+44,2); // conventional Z, bottom-up
            header.putDouble(d+48,p.x());header.putDouble(d+56,p.y());header.putDouble(d+64,p.z());
            header.putFloat(d+72,p.yaw());header.putFloat(d+76,p.pitch());header.putFloat(d+80,0);header.putInt(d+84,p.firstPerson()?1:0);
            header.putLong(d+88,c.captured);header.putLong(d+96,System.nanoTime());java.lang.invoke.VarHandle.fullFence();
            header.putLong(d,(seq+2)&~1L);header.putInt(40,slot);header.putLong(32,++published);slot=(slot+1)%SLOTS;
            lastPublishedFrame=c.number;
            GL32.glDeleteSync(c.fence);c.fence=0;
        }
    }
}
