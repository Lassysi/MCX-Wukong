package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.HostTerrainTarget;
import net.minecraft.world.InteractionResult;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.context.UseOnContext;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(ItemStack.class)
abstract class HostTerrainUseMixin {
    @Inject(method="useOn",at=@At("HEAD"),cancellable=true)
    private void dqj$surfaceUse(UseOnContext context,CallbackInfoReturnable<InteractionResult> ci){
        if(!HostTerrainTarget.allowUse(context))ci.setReturnValue(InteractionResult.FAIL);
    }
}
