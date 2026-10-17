package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.world.level.BlockGetter;
import net.minecraft.world.level.ClipContext;
import net.minecraft.world.phys.BlockHitResult;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(BlockGetter.class)
interface HostBlockClipMixin {
    @Inject(method="clip(Lnet/minecraft/world/level/ClipContext;)Lnet/minecraft/world/phys/BlockHitResult;",at=@At("RETURN"),cancellable=true)
    private void dqj$crossCell(ClipContext context,CallbackInfoReturnable<BlockHitResult> ci){
        ci.setReturnValue(HostBlockAnchors.clip((BlockGetter)this,context,ci.getReturnValue()));
    }
}
