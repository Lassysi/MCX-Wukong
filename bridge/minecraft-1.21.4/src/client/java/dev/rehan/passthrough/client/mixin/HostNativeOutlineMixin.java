package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.client.NativeBlocks;
import net.minecraft.client.renderer.LevelRenderer;
import net.minecraft.core.BlockPos;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Preview86: the selection outline of a Wukong-drawn block is drawn by the host in the same frame;
 *  MC's own outline (MC layer) would drift against it. */
@Mixin(LevelRenderer.class)
abstract class HostNativeOutlineMixin {
    @Inject(method="renderHitOutline",at=@At("HEAD"),cancellable=true)
    private void dqj$native(com.mojang.blaze3d.vertex.PoseStack pose,com.mojang.blaze3d.vertex.VertexConsumer consumer,net.minecraft.world.entity.Entity entity,
                            double camX,double camY,double camZ,BlockPos pos,net.minecraft.world.level.block.state.BlockState state,int color,CallbackInfo ci){
        if(NativeBlocks.drawn(pos))ci.cancel();
    }
}
