package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.ProxyFx;
import net.minecraft.world.entity.Entity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Keep bridge capture exclusions and cooldown effective on vanilla automatic boarding too. */
@Mixin(Entity.class)
abstract class HostProxyMountMixin {
    @Inject(method="startRiding(Lnet/minecraft/world/entity/Entity;Z)Z",at=@At("HEAD"),cancellable=true)
    private void dqj$mountGate(Entity vehicle,boolean force,CallbackInfoReturnable<Boolean> cir){
        if(!ProxyFx.mayBoard((Entity)(Object)this,vehicle))cir.setReturnValue(false);
    }
}
