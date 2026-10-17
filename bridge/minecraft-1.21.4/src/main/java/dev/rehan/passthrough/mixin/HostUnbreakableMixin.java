package dev.rehan.passthrough.mixin;

import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.item.ItemStack;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Preview91 (user: crafting is not part of this setup, so worn-out gear cannot be replaced): every item keeps
 *  full durability. All vanilla wear (weapons, tools, armor, shields, elytra, bows, crossbow-based guns) passes
 *  through processDurabilityChange; the gun mod's ammo bar uses set_damage and is unaffected. */
@Mixin(ItemStack.class)
abstract class HostUnbreakableMixin {
    @Inject(method="processDurabilityChange",at=@At("HEAD"),cancellable=true)
    private void dqj$noWear(int amount,ServerLevel level,ServerPlayer player,CallbackInfoReturnable<Integer> cir){
        if(amount>0)cir.setReturnValue(0);
    }
}
