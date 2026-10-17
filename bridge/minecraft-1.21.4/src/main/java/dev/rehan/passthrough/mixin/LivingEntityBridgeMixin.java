package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.CombatBridge;
import java.util.ArrayDeque;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.entity.*;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.*;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(LivingEntity.class)
abstract class LivingEntityBridgeMixin {
    @Unique private final ArrayDeque<Float> dqj$before=new ArrayDeque<>();
    @Unique private final ArrayDeque<Float> dqj$beforeHeal=new ArrayDeque<>();
    // Preview89: weapon balance for hits on Wukong enemy proxies (see Balance).
    @org.spongepowered.asm.mixin.injection.ModifyVariable(method="hurtServer",at=@At("HEAD"),argsOnly=true)
    private float dqj$balance(float amount,@com.llamalad7.mixinextras.sugar.Local(argsOnly=true) DamageSource source){
        var self=(LivingEntity)(Object)this;
        // Preview92 (live: elytra take-offs killed the player with fly_into_wall): the bridge's terrain is coarse
        // 1-block voxels, so gliding "into a wall" is mostly an artefact; no kinetic damage while bridged.
        if(self instanceof net.minecraft.server.level.ServerPlayer&&dev.rehan.passthrough.Passthrough.active
            &&source.is(net.minecraft.world.damagesource.DamageTypes.FLY_INTO_WALL))return 0f;
        return CombatBridge.isProxy(self)?dev.rehan.passthrough.Balance.outgoing(self,source,amount):amount;
    }
    @Inject(method="hurtServer",at=@At("HEAD"))
    private void dqj$beforeDamage(ServerLevel level,DamageSource source,float amount,CallbackInfoReturnable<Boolean> cir){
        dqj$before.push(((LivingEntity)(Object)this).getHealth());
    }
    @Inject(method="hurtServer",at=@At("RETURN"))
    private void dqj$afterDamage(ServerLevel level,DamageSource source,float amount,CallbackInfoReturnable<Boolean> cir){
        CombatBridge.damage((LivingEntity)(Object)this,source,dqj$before.pop());
    }
    @Inject(method="heal",at=@At("HEAD"))
    private void dqj$beforeHeal(float amount,CallbackInfo ci){dqj$beforeHeal.push(((LivingEntity)(Object)this).getHealth());}
    @Inject(method="heal",at=@At("RETURN"))
    private void dqj$afterHeal(float amount,CallbackInfo ci){CombatBridge.healed((LivingEntity)(Object)this,dqj$beforeHeal.pop());}
    @Inject(method="getDefaultDimensions",at=@At("HEAD"),cancellable=true)
    private void dqj$hostHitbox(Pose pose,CallbackInfoReturnable<EntityDimensions> cir){
        var d=CombatBridge.dimensions((LivingEntity)(Object)this);if(d!=null)cir.setReturnValue(d);
    }
}
