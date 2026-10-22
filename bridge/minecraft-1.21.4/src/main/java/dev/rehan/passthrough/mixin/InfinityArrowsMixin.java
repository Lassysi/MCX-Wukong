package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.Passthrough;
import net.minecraft.core.registries.Registries;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.item.BowItem;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.enchantment.EnchantmentHelper;
import net.minecraft.world.item.enchantment.Enchantments;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Preview120 (user: an Infinity bow still used up all my arrows): vanilla Infinity only spares plain arrows, so tipped
 *  arrows (slowness, poison, harming from the shrine shop) were consumed. While bridged, an Infinity bow spares every
 *  arrow type. At least one arrow must still be in the inventory (vanilla rule, kept by request). The shot arrow is
 *  then intangible (vanilla), so it can't be picked up to duplicate arrows. */
@Mixin(EnchantmentHelper.class)
abstract class InfinityArrowsMixin {
    @Inject(method="processAmmoUse",at=@At("HEAD"),cancellable=true)
    private static void dqj$infinitySparesAllArrows(ServerLevel level,ItemStack weapon,ItemStack ammo,int count,CallbackInfoReturnable<Integer> cir){
        if(!Passthrough.active||!(weapon.getItem() instanceof BowItem))return;
        var infinity=level.registryAccess().lookupOrThrow(Registries.ENCHANTMENT).get(Enchantments.INFINITY);
        if(infinity.isPresent()&&EnchantmentHelper.getItemEnchantmentLevel(infinity.get(),weapon)>0)cir.setReturnValue(0);
    }
}
