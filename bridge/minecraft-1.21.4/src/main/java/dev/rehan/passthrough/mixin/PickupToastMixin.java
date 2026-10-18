package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.PickupToasts;
import net.minecraft.world.entity.item.ItemEntity;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.item.ItemStack;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Preview106: an item entity picked up by the player (MC drops, items thrown back) shows in the right-side
 *  pickup list. Server side only; the amount is what actually left the entity. */
@Mixin(ItemEntity.class)
abstract class PickupToastMixin {
    @Unique private ItemStack dqj$before=ItemStack.EMPTY;
    @Inject(method="playerTouch",at=@At("HEAD"))
    private void dqj$before(Player player,CallbackInfo ci){
        dqj$before=player.level().isClientSide?ItemStack.EMPTY:((ItemEntity)(Object)this).getItem().copy();
    }
    @Inject(method="playerTouch",at=@At("TAIL"))
    private void dqj$after(Player player,CallbackInfo ci){
        if(dqj$before.isEmpty())return;
        var self=(ItemEntity)(Object)this;
        int left=self.isRemoved()?0:self.getItem().getCount();
        int taken=dqj$before.getCount()-left;
        if(taken>0)PickupToasts.push(dqj$before,taken);
        dqj$before=ItemStack.EMPTY;
    }
}
