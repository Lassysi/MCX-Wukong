package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.Balance;
import net.minecraft.world.InteractionHand;
import net.minecraft.world.InteractionResult;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.item.ShieldItem;
import net.minecraft.world.level.Level;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Live114 (user: bow/crossbow and the off-hand shield fight over right-click, but the shield should stay in the off
 *  hand for sword fights): while a ranged weapon is in the main hand, the off-hand shield never raises. */
@Mixin(ShieldItem.class)
abstract class ShieldRangedMixin {
    @Inject(method="use",at=@At("HEAD"),cancellable=true)
    private void dqj$noShieldWithRanged(Level level,Player player,InteractionHand hand,CallbackInfoReturnable<InteractionResult> cir){
        if(hand==InteractionHand.OFF_HAND&&Balance.rangedMain(player))cir.setReturnValue(InteractionResult.PASS);
    }
}
