package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.Passthrough;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.food.FoodData;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Host gameplay has no hunger mechanic. Item/effect healing remains native. */
@Mixin(FoodData.class)
abstract class HostFoodMixin {
    @Shadow private int foodLevel,tickTimer;
    @Shadow private float saturationLevel,exhaustionLevel;
    @Unique private boolean dqj$leased;
    @Unique private int dqj$food,dqj$timer;
    @Unique private float dqj$saturation,dqj$exhaustion;
    @Inject(method="tick",at=@At("HEAD"),cancellable=true)
    private void dqj$hostFood(ServerPlayer player,CallbackInfo ci){
        if(Passthrough.active){
            if(!dqj$leased){dqj$leased=true;dqj$food=foodLevel;dqj$timer=tickTimer;dqj$saturation=saturationLevel;dqj$exhaustion=exhaustionLevel;}
            foodLevel=20;saturationLevel=5;exhaustionLevel=0;tickTimer=0;ci.cancel();
        }else if(dqj$leased){
            foodLevel=dqj$food;saturationLevel=dqj$saturation;exhaustionLevel=dqj$exhaustion;tickTimer=dqj$timer;dqj$leased=false;
        }
    }
}
