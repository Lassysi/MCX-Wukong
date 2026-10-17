package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.Passthrough;
import net.minecraft.client.Camera;
import net.minecraft.client.renderer.FogParameters;
import net.minecraft.client.renderer.FogRenderer;
import org.joml.Vector4f;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Preview76 (BUG-04: distant blocks became a light-blue patch). The lab renders 2 chunks, so
 *  MC's terrain fog starts ~28 blocks out and paints blocks the sky colour. While the bridge is
 *  up the compositor applies Wukong's own haze, so MC draws no fog at all. */
@Mixin(FogRenderer.class)
abstract class HostNoFogMixin {
    @Inject(method="setupFog",at=@At("HEAD"),cancellable=true)
    private static void dqj$noFog(Camera camera,FogRenderer.FogMode mode,Vector4f color,float distance,boolean thick,float partial,CallbackInfoReturnable<FogParameters> ci){
        if(Passthrough.active)ci.setReturnValue(FogParameters.NO_FOG);
    }
}
