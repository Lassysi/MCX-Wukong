package dev.rehan.passthrough.client.mixin;

import com.mojang.blaze3d.vertex.PoseStack;
import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.client.renderer.MultiBufferSource;
import net.minecraft.client.renderer.blockentity.BlockEntityRenderDispatcher;
import net.minecraft.world.level.block.entity.BlockEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Preview48: chests are drawn by their block-entity renderer, not the block model.
 *  LevelRenderer pushes/pops the pose around this call, so the shift stays local. */
@Mixin(BlockEntityRenderDispatcher.class)
abstract class HostBlockEntityRenderMixin {
    @Inject(method="render",at=@At("HEAD"))
    private <E extends BlockEntity> void dqj$shift(E blockEntity,float partial,PoseStack pose,MultiBufferSource buffers,CallbackInfo ci){
        var level=blockEntity.getLevel();
        if(level==null||!HostBlockAnchors.world(level))return;
        var offset=HostBlockAnchors.offset(blockEntity.getBlockState(),blockEntity.getBlockPos());
        if(offset!=net.minecraft.world.phys.Vec3.ZERO)pose.translate(offset.x,offset.y,offset.z);
    }
}
