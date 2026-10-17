package dev.rehan.passthrough.client.mixin;

import com.llamalad7.mixinextras.injector.wrapoperation.Operation;
import com.llamalad7.mixinextras.injector.wrapoperation.WrapOperation;
import dev.rehan.passthrough.HostBlockAnchors;
import net.fabricmc.fabric.impl.client.indigo.renderer.render.TerrainRenderContext;
import net.minecraft.client.Minecraft;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;

@Mixin(value=TerrainRenderContext.class,remap=false)
abstract class HostIndigoTerrainMixin {
    // Preview84: a block the host draws natively is left out of the MC chunk mesh.
    @org.spongepowered.asm.mixin.injection.Inject(method="tessellateBlock",remap=true,at=@At("HEAD"),cancellable=true)
    private void dqj$native(BlockState state,BlockPos pos,net.minecraft.client.resources.model.BakedModel model,com.mojang.blaze3d.vertex.PoseStack matrices,
                            org.spongepowered.asm.mixin.injection.callback.CallbackInfo ci){
        if(dev.rehan.passthrough.client.NativeBlocks.drawn(pos))ci.cancel();
    }
    @WrapOperation(method="tessellateBlock",remap=true,at=@At(value="INVOKE",target="Lnet/minecraft/world/level/block/state/BlockState;getOffset(Lnet/minecraft/core/BlockPos;)Lnet/minecraft/world/phys/Vec3;",remap=true))
    private Vec3 dqj$offset(BlockState state,BlockPos pos,Operation<Vec3> original){
        Vec3 base=original.call(state,pos);var level=Minecraft.getInstance().level;
        return level!=null&&HostBlockAnchors.world(level)?base.add(HostBlockAnchors.offset(state,pos)):base;
    }
}
