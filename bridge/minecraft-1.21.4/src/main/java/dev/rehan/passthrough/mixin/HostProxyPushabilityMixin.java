package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.CombatBridge;
import net.minecraft.world.entity.LivingEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Exclude proxy bodies from vanilla push-candidate queries without changing pickability or attack knockback. */
@Mixin(LivingEntity.class)
abstract class HostProxyPushabilityMixin {
    @Inject(method="isPushable",at=@At("HEAD"),cancellable=true)
    private void dqj$notAPushBody(CallbackInfoReturnable<Boolean> cir){
        if(CombatBridge.isProxy((LivingEntity)(Object)this))cir.setReturnValue(false);
    }
}
