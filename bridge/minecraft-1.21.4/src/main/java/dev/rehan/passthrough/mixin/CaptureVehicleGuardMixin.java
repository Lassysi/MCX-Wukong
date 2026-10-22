package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.Passthrough;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.entity.vehicle.VehicleEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Preview124 (user: the boat/minecart holding a monster can only be broken by us): while bridged, boats and minecarts take
 *  damage only from a player (melee, arrows, TNT or guns the player owns); monster blasts, other mobs and the captured
 *  monster itself cannot break them. */
@Mixin(VehicleEntity.class)
abstract class CaptureVehicleGuardMixin {
    @Inject(method="hurtServer",at=@At("HEAD"),cancellable=true)
    private void dqj$onlyPlayerBreaks(ServerLevel level,DamageSource source,float amount,CallbackInfoReturnable<Boolean> cir){
        if(Passthrough.active&&!(source.getEntity() instanceof Player))cir.setReturnValue(false);
    }
}
