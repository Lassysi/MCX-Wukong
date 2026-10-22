package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.Passthrough;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.Items;
import net.minecraft.world.level.Level;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Potions & items D1: consuming certain items also acts on the Wukong side.
 *  - Enchanted golden apple: instantly full health (user), plus vanilla Regeneration/Absorption/Resistance/Fire Res.
 *    The MC -> native health sync carries the full heal to Wukong's HP.
 *  - Milk bucket / honey bottle: tell the host to cure native status damage (burn/poison ticks) for a while. */
@Mixin(ItemStack.class)
abstract class ConsumeHooksMixin {
    @Unique private static final ThreadLocal<String> dqj$eating=new ThreadLocal<>();
    @Inject(method="finishUsingItem",at=@At("HEAD"))
    private void dqj$before(Level level,LivingEntity entity,CallbackInfoReturnable<ItemStack> cir){
        ItemStack self=(ItemStack)(Object)this;
        dqj$eating.set(level.isClientSide||!(entity instanceof ServerPlayer)?null:
            self.is(Items.ENCHANTED_GOLDEN_APPLE)?"apple":self.is(Items.MILK_BUCKET)?"milk":self.is(Items.HONEY_BOTTLE)?"honey":null);
    }
    @Inject(method="finishUsingItem",at=@At("RETURN"))
    private void dqj$after(Level level,LivingEntity entity,CallbackInfoReturnable<ItemStack> cir){
        String kind=dqj$eating.get();dqj$eating.remove();
        if(kind==null||!Passthrough.active)return;
        if(kind.equals("apple")){dev.rehan.passthrough.GoldenApple.consumed((ServerPlayer)entity);}
        else{Passthrough.events.accept("{\"t\":\"player_cure\",\"kind\":\""+kind+"\"}");Passthrough.LOG.info("DQJ items: {} -> native status cure requested",kind);}
    }
}
