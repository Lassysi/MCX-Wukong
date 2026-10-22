package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.CombatBridge;
import net.minecraft.world.entity.Entity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** A hit-assist proxy is not a physical mob. noPhysics is server-local, so guard both push directions on both sides. */
@Mixin(Entity.class)
abstract class HostProxyPushMixin {
    @Inject(method="push(Lnet/minecraft/world/entity/Entity;)V",at=@At("HEAD"),cancellable=true)
    private void dqj$noProxyBodyPush(Entity other,CallbackInfo ci){
        if(CombatBridge.isProxy((Entity)(Object)this)||CombatBridge.isProxy(other))ci.cancel();
    }
}
