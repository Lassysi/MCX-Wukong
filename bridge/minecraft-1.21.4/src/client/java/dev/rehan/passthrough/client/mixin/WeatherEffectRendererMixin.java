package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.NativeControl;
import net.minecraft.client.renderer.WeatherEffectRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Host weather owns the scene; the MC world still keeps its native weather state. */
@Mixin(WeatherEffectRenderer.class)
abstract class WeatherEffectRendererMixin {
    @Inject(method="render(Lnet/minecraft/world/level/Level;Lnet/minecraft/client/renderer/MultiBufferSource;IFLnet/minecraft/world/phys/Vec3;)V", at=@At("HEAD"), cancellable=true)
    private void dqj$hostPrecipitation(CallbackInfo ci) {
        if (Passthrough.minecraftAuthority) { NativeControl.weatherPassesSuppressed++; ci.cancel(); }
    }

    @Inject(method="tickRainParticles", at=@At("HEAD"), cancellable=true)
    private void dqj$hostRainParticles(CallbackInfo ci) {
        if (Passthrough.minecraftAuthority) { NativeControl.rainParticleTicksSuppressed++; ci.cancel(); }
    }
}
