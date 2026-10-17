package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.CombatBridge;
import net.minecraft.world.entity.LivingEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** C1: host enemy proxies never interpolate on the client (vanilla lerps 3 ticks). */
@Mixin(LivingEntity.class)
abstract class HostProxyLerpMixin {
    @Inject(method="lerpTo",at=@At("HEAD"),cancellable=true)
    private void dqj$snap(double x,double y,double z,float yRot,float xRot,int steps,CallbackInfo ci){
        var self=(LivingEntity)(Object)this;
        if(!CombatBridge.isProxy(self))return;
        self.setPos(x,y,z);self.setOldPosAndRot();ci.cancel();
    }
}
