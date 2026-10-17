package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.HostBlockAnchors;
import net.fabricmc.fabric.impl.client.indigo.renderer.render.BlockRenderInfo;
import net.minecraft.client.Minecraft;
import net.minecraft.core.BlockPos;
import net.minecraft.core.Direction;
import net.minecraft.world.level.BlockAndTintGetter;
import net.minecraft.world.level.block.state.BlockState;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(value=BlockRenderInfo.class,remap=false)
abstract class HostIndigoFaceMixin {
    @Shadow public BlockPos blockPos;
    @Shadow public BlockState blockState;
    @Shadow public BlockAndTintGetter blockView;
    @Inject(method="shouldDrawSide",at=@At("HEAD"),cancellable=true)
    private void dqj$face(Direction side,CallbackInfoReturnable<Boolean> ci){
        var level=Minecraft.getInstance().level;
        if(side!=null&&level!=null&&HostBlockAnchors.world(level)&&blockPos!=null){
            var neighbour=blockPos.relative(side);
            if(HostBlockAnchors.offset(blockState,blockPos).distanceToSqr(HostBlockAnchors.offset(blockView.getBlockState(neighbour),neighbour))>1e-10)ci.setReturnValue(true);
        }
    }
}
