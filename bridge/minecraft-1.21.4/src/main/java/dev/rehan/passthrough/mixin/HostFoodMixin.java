package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.Balance;
import dev.rehan.passthrough.Passthrough;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.food.FoodData;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.ModifyVariable;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Until Preview106 hunger was frozen at full while bridged (no food existed in the setup).
 *  Preview107 (user: food is in the shop now, so enable hunger, but drain it far slower because Wukong is a lot of
 *  running; attacks and shield blocks cost a tiny amount):
 *  - every exhaustion source (sprint, jump, attack, regeneration, our shield cost) is scaled by
 *    Balance.hungerScale (default 0.1, config "hunger.exhaustionScale");
 *  - no starvation damage at empty hunger (config "hunger.starvation"): empty hunger only stops regen and sprinting.
 *  Live107 (user: full-hunger regeneration heals far too fast; hunger is almost always full): vanilla regeneration is
 *  replaced while bridged. Health regenerates only after Balance.regenDelayTicks (default 60 s) without taking any
 *  damage, then half a heart every Balance.regenIntervalTicks (default 80 ticks = 4 s) while food >= 18; each heal costs
 *  vanilla's 6 exhaustion (scaled). The healing reaches the native HP through the existing MC -> native health sync. */
@Mixin(FoodData.class)
abstract class HostFoodMixin {
    @Shadow private int foodLevel,tickTimer;
    @Shadow public abstract void addExhaustion(float exhaustion);
    @Unique private float dqj$lastHealth=-1;
    @Unique private long dqj$lastHurt;
    @Inject(method="tick",at=@At("HEAD"))
    private void dqj$slowRegen(ServerPlayer player,CallbackInfo ci){
        if(!Passthrough.active){dqj$lastHealth=-1;return;}
        tickTimer=0; // vanilla regeneration (both paths) and starvation never trigger while bridged
        long now=player.serverLevel().getGameTime();float hp=player.getHealth();
        if(dqj$lastHealth<0||hp<dqj$lastHealth-.01f)dqj$lastHurt=now;
        dqj$lastHealth=hp;
        if(foodLevel>=18&&player.isHurt()&&now-dqj$lastHurt>=Balance.regenDelayTicks()&&now%Math.max(1,Balance.regenIntervalTicks())==0){
            player.heal(1f);addExhaustion(6f);dqj$lastHealth=player.getHealth();
        }
    }
    @ModifyVariable(method="addExhaustion",at=@At("HEAD"),argsOnly=true,ordinal=0)
    private float dqj$slowHunger(float exhaustion){
        return Passthrough.active&&exhaustion>0?exhaustion*(float)Balance.hungerScale():exhaustion;
    }
}
