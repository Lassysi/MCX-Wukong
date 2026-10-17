package dev.rehan.passthrough.client.mixin;

import com.mojang.blaze3d.systems.RenderSystem;
import com.mojang.blaze3d.platform.GlStateManager;
import org.lwjgl.opengl.GL11;
import dev.rehan.passthrough.client.FrameExporter;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Vanilla RGB-only hand passes must also write coverage into the transparent export target. */
@Mixin(RenderSystem.class)
abstract class OverlayColorMaskMixin {
    @Inject(method="colorMask",at=@At("HEAD"),cancellable=true,remap=false)
    private static void dqj$handCoverage(boolean r,boolean g,boolean b,boolean a,CallbackInfo ci) {
        if(FrameExporter.handPass && r && g && b && !a){RenderSystem.colorMask(r,g,b,true);ci.cancel();}
    }
    @Inject(method="blendFunc(Lcom/mojang/blaze3d/platform/GlStateManager$SourceFactor;Lcom/mojang/blaze3d/platform/GlStateManager$DestFactor;)V",
        at=@At("HEAD"),cancellable=true,remap=false)
    private static void dqj$vignetteCoverage(GlStateManager.SourceFactor src,GlStateManager.DestFactor dst,CallbackInfo ci) {
        if(FrameExporter.capturingOverlay() && src==GlStateManager.SourceFactor.ZERO && dst==GlStateManager.DestFactor.ONE_MINUS_SRC_COLOR){
            // Vignette keeps its native RGB operation; its full-screen pass must preserve coverage.
            RenderSystem.blendFuncSeparate(GL11.GL_ZERO,GL11.GL_ONE_MINUS_SRC_COLOR,GL11.GL_ZERO,GL11.GL_ONE);ci.cancel();
        }
    }
}
