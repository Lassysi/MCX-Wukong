package dev.rehan.passthrough.mixin;

import com.llamalad7.mixinextras.injector.wrapmethod.WrapMethod;
import com.llamalad7.mixinextras.injector.wrapoperation.Operation;
import dev.rehan.passthrough.HostBlockAnchors;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.BlockGetter;
import net.minecraft.world.level.block.state.BlockBehaviour;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.phys.shapes.CollisionContext;
import net.minecraft.world.phys.shapes.VoxelShape;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Preview48: shift each anchored block's own shape (full cube, slab, stair, chest).
 *  Preview52: the getters call each other, so only the outermost call shifts. */
@Mixin(BlockBehaviour.BlockStateBase.class)
abstract class HostBlockAnchorMixin {
    @WrapMethod(method="getCollisionShape(Lnet/minecraft/world/level/BlockGetter;Lnet/minecraft/core/BlockPos;)Lnet/minecraft/world/phys/shapes/VoxelShape;")
    private VoxelShape dqj$collision(BlockGetter level,BlockPos pos,Operation<VoxelShape> original){
        return HostBlockAnchors.outermost(()->original.call(level,pos),(BlockState)(Object)this,level,pos);
    }
    @WrapMethod(method="getCollisionShape(Lnet/minecraft/world/level/BlockGetter;Lnet/minecraft/core/BlockPos;Lnet/minecraft/world/phys/shapes/CollisionContext;)Lnet/minecraft/world/phys/shapes/VoxelShape;")
    private VoxelShape dqj$collisionContext(BlockGetter level,BlockPos pos,CollisionContext context,Operation<VoxelShape> original){
        return HostBlockAnchors.outermost(()->original.call(level,pos,context),(BlockState)(Object)this,level,pos);
    }
    @WrapMethod(method="getShape(Lnet/minecraft/world/level/BlockGetter;Lnet/minecraft/core/BlockPos;)Lnet/minecraft/world/phys/shapes/VoxelShape;")
    private VoxelShape dqj$shape(BlockGetter level,BlockPos pos,Operation<VoxelShape> original){
        return HostBlockAnchors.outermost(()->original.call(level,pos),(BlockState)(Object)this,level,pos);
    }
    @WrapMethod(method="getShape(Lnet/minecraft/world/level/BlockGetter;Lnet/minecraft/core/BlockPos;Lnet/minecraft/world/phys/shapes/CollisionContext;)Lnet/minecraft/world/phys/shapes/VoxelShape;")
    private VoxelShape dqj$shapeContext(BlockGetter level,BlockPos pos,CollisionContext context,Operation<VoxelShape> original){
        return HostBlockAnchors.outermost(()->original.call(level,pos,context),(BlockState)(Object)this,level,pos);
    }
    @WrapMethod(method="getInteractionShape")
    private VoxelShape dqj$interaction(BlockGetter level,BlockPos pos,Operation<VoxelShape> original){
        return HostBlockAnchors.outermost(()->original.call(level,pos),(BlockState)(Object)this,level,pos);
    }
    @Inject(method="isCollisionShapeFullBlock",at=@At("HEAD"),cancellable=true)
    private void dqj$full(BlockGetter level,BlockPos pos,CallbackInfoReturnable<Boolean> ci){
        if(HostBlockAnchors.shape((BlockState)(Object)this,level,pos)!=null)ci.setReturnValue(false);
    }
    @Inject(method="hasLargeCollisionShape",at=@At("HEAD"),cancellable=true)
    private void dqj$large(CallbackInfoReturnable<Boolean> ci){
        if(HostBlockAnchors.hasAny()&&HostBlockAnchors.eligible(((BlockState)(Object)this).getBlock()))ci.setReturnValue(true);
    }
}
