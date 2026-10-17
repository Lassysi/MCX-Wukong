package dev.rehan.passthrough.client.mixin;

import com.llamalad7.mixinextras.injector.wrapoperation.Operation;
import com.llamalad7.mixinextras.injector.wrapoperation.WrapOperation;
import com.llamalad7.mixinextras.sugar.Local;
import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.block.ModelBlockRenderer;
import net.minecraft.core.BlockPos;
import net.minecraft.core.Direction;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;

@Mixin(ModelBlockRenderer.class)
abstract class HostBlockModelMixin {
    private static boolean dqj$overworld(){var level=Minecraft.getInstance().level;return level!=null&&HostBlockAnchors.world(level);}
    @WrapOperation(method="tesselateBlock",at=@At(value="INVOKE",target="Lnet/minecraft/world/level/block/state/BlockState;getOffset(Lnet/minecraft/core/BlockPos;)Lnet/minecraft/world/phys/Vec3;"))
    private Vec3 dqj$offset(BlockState state,BlockPos pos,Operation<Vec3> original){
        Vec3 base=original.call(state,pos);
        return dqj$overworld()?base.add(HostBlockAnchors.offset(state,pos)):base;
    }
    @WrapOperation(method={"tesselateWithAO","tesselateWithoutAO"},at=@At(value="INVOKE",target="Lnet/minecraft/world/level/block/Block;shouldRenderFace(Lnet/minecraft/world/level/block/state/BlockState;Lnet/minecraft/world/level/block/state/BlockState;Lnet/minecraft/core/Direction;)Z"))
    private boolean dqj$face(BlockState state,BlockState neighbour,Direction side,Operation<Boolean> original,@Local(argsOnly=true) BlockPos pos){
        if(dqj$overworld()&&HostBlockAnchors.offset(state,pos).distanceToSqr(HostBlockAnchors.offset(neighbour,pos.relative(side)))>1e-10)return true;
        return original.call(state,neighbour,side);
    }
}
