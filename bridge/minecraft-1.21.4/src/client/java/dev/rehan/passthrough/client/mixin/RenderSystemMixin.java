package dev.rehan.passthrough.client.mixin;

import com.mojang.blaze3d.systems.RenderSystem;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.HostState;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.GL11;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Redirect;

@Mixin(RenderSystem.class)
abstract class RenderSystemMixin {
    @Redirect(method="flipFrame", at=@At(value="INVOKE", target="Lorg/lwjgl/glfw/GLFW;glfwSwapBuffers(J)V", remap=false))
    private static void dqj$presentThroughHost(long window) {
        HostState.refresh();
        // Rendering and PBO export still run. Polling/replay/Tracy in flipFrame stay intact.
        // Submit offscreen work without waiting for an occluded second swap chain.
        if (Passthrough.active) GL11.glFlush();
        else GLFW.glfwSwapBuffers(window);
    }
}
