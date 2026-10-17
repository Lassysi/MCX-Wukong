package dev.rehan.passthrough.client;

import com.google.gson.JsonObject;
import dev.rehan.passthrough.Passthrough;
import net.minecraft.client.Minecraft;
import org.lwjgl.glfw.GLFW;

/** Resize the background renderer without restoring, activating or raising its window. */
public final class BridgeWindow {
    private static long requests,resizes,deferred,rejected,focusGains;
    private BridgeWindow() {}

    public static void focused(boolean focused){if(focused)focusGains++;}

    static void resize(int width,int height){
        requests++;
        if(width<320||height<240||width>3840||height>2160){rejected++;return;}
        long handle=Minecraft.getInstance().getWindow().getWindow();
        int[] w={0},h={0};GLFW.glfwGetWindowSize(handle,w,h);
        if(w[0]==width&&h[0]==height)return;
        // Respect explicit user window modes. Restoring from any of these can
        // change Windows focus/z-order and is unrelated to a viewport request.
        if(GLFW.glfwGetWindowMonitor(handle)!=0
            ||GLFW.glfwGetWindowAttrib(handle,GLFW.GLFW_ICONIFIED)==GLFW.GLFW_TRUE
            ||GLFW.glfwGetWindowAttrib(handle,GLFW.GLFW_MAXIMIZED)==GLFW.GLFW_TRUE){deferred++;return;}
        // Window.setWindowed also calls setMode/glfwSetWindowMonitor. A normal
        // window needs only size; vanilla's resize callbacks update its viewport.
        GLFW.glfwSetWindowSize(handle,width,height);resizes++;
        Passthrough.LOG.info("DQJ background viewport resize {}x{}; window mode and focus retained",width,height);
    }

    static JsonObject report(){
        var mc=Minecraft.getInstance();long handle=mc.getWindow().getWindow();
        int[] w={0},h={0};GLFW.glfwGetWindowSize(handle,w,h);
        var out=new JsonObject();out.addProperty("width",w[0]);out.addProperty("height",h[0]);
        out.addProperty("requests",requests);out.addProperty("resizes",resizes);out.addProperty("deferred",deferred);out.addProperty("rejected",rejected);
        out.addProperty("focusGains",focusGains);out.addProperty("focused",GLFW.glfwGetWindowAttrib(handle,GLFW.GLFW_FOCUSED)==GLFW.GLFW_TRUE);
        out.addProperty("maximized",GLFW.glfwGetWindowAttrib(handle,GLFW.GLFW_MAXIMIZED)==GLFW.GLFW_TRUE);
        out.addProperty("iconified",mc.getWindow().isIconified());return out;
    }
}
