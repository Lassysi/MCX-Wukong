package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.NativePause;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Server noGravity is not synced. Hold the integrated client too, so it cannot walk/fall behind native play. */
@Mixin(LocalPlayer.class)
abstract class NativePauseMixin {
    @Inject(method="aiStep",at=@At("HEAD"),cancellable=true)
    private void dqj$nativePause(CallbackInfo ci){
        var p=(LocalPlayer)(Object)this;var at=NativePause.position(p.getUUID());
        if(at!=null){p.setPos(at);p.setDeltaMovement(Vec3.ZERO);p.fallDistance=0;ci.cancel();}
    }
}
