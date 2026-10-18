package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.Balance;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.item.ItemStack;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.ModifyVariable;

/** Preview91 made every item unbreakable (no crafting to replace worn-out gear).
 *  Preview105 (user: bring durability back, slowly, repair at the shrine with emeralds): each point of vanilla wear
 *  (weapons, tools, armor, shields, elytra, bows) is applied only with probability Balance.wearRate (default 0.2),
 *  before Unbreaking, so a normal chapter barely dents good gear. Guns keep no wear: the gun mod's ammo bar uses
 *  set_damage and its crossbow base must never break. Repair is ShrineShop.rest(). */
@Mixin(ItemStack.class)
abstract class HostUnbreakableMixin {
    @ModifyVariable(method="processDurabilityChange",at=@At("HEAD"),argsOnly=true,ordinal=0)
    private int dqj$slowWear(int amount,int original,ServerLevel level,ServerPlayer player){
        if(amount<=0)return amount;
        ItemStack self=(ItemStack)(Object)this;
        if(Balance.isGun(self))return 0;
        double rate=Balance.wearRate();
        if(rate>=1)return amount;
        int kept=0;for(int i=0;i<amount;i++)if(level.random.nextDouble()<rate)kept++;
        return kept;
    }
}
