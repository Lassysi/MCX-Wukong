package dev.rehan.passthrough.client.mixin;

import com.mojang.blaze3d.platform.Window;
import dev.rehan.passthrough.client.BridgeWindow;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.GuiPointerExtent;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Observe actual GLFW focus callbacks, including brief focus gains between state probes. */
@Mixin(Window.class)
abstract class WindowFocusMixin {
    @Shadow private int width,height,framebufferWidth,framebufferHeight;
    @Unique private int dqj$lastLogicalWidth,dqj$lastLogicalHeight;
    @Inject(method="<init>",at=@At("TAIL"))
    private void dqj$initialLogicalSize(CallbackInfo ci){dqj$remember(width,height);}
    @Inject(method="onResize",at=@At("HEAD"))
    private void dqj$logicalResize(long handle,int w,int h,CallbackInfo ci){
        if(handle==((Window)(Object)this).getWindow())dqj$remember(w,h);
    }
    @Unique private void dqj$remember(int w,int h){if(w>0&&h>0){dqj$lastLogicalWidth=w;dqj$lastLogicalHeight=h;}}
    @Inject(method="getScreenWidth",at=@At("HEAD"),cancellable=true)
    private void dqj$guiWidth(CallbackInfoReturnable<Integer> cir){
        if(width<=0&&Passthrough.minecraftAuthority)cir.setReturnValue(GuiPointerExtent.dimension(width,dqj$lastLogicalWidth,framebufferWidth,true));
    }
    @Inject(method="getScreenHeight",at=@At("HEAD"),cancellable=true)
    private void dqj$guiHeight(CallbackInfoReturnable<Integer> cir){
        if(height<=0&&Passthrough.minecraftAuthority)cir.setReturnValue(GuiPointerExtent.dimension(height,dqj$lastLogicalHeight,framebufferHeight,true));
    }
    @Inject(method="onFocus",at=@At("HEAD"))
    private void dqj$focus(long handle,boolean focused,CallbackInfo ci){
        if(handle==((Window)(Object)this).getWindow())BridgeWindow.focused(focused);
    }
}
