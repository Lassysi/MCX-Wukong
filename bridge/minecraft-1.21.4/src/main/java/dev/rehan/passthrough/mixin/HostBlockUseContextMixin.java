package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.world.item.context.UseOnContext;
import net.minecraft.world.phys.BlockHitResult;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Final;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(UseOnContext.class)
abstract class HostBlockUseContextMixin {
    @Shadow @Final private BlockHitResult hitResult;
    @Inject(method="getClickLocation",at=@At("RETURN"),cancellable=true)
    private void dqj$canonicalPoint(CallbackInfoReturnable<Vec3> ci){
        var context=(UseOnContext)(Object)this;
        if(HostBlockAnchors.world(context.getLevel())){
            var offset=HostBlockAnchors.offset(context.getLevel().getBlockState(hitResult.getBlockPos()),hitResult.getBlockPos());
            if(offset!=Vec3.ZERO)ci.setReturnValue(ci.getReturnValue().subtract(offset));
        }
    }
}
