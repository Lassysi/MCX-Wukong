package dev.rehan.passthrough.mixin;

import dev.rehan.passthrough.WorldBridge;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.BlockGetter;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.block.Blocks;
import net.minecraft.world.level.block.state.BlockBehaviour;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.phys.shapes.CollisionContext;
import net.minecraft.world.phys.shapes.VoxelShape;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Native MC collision, picking and interaction consume the same measured floor. */
@Mixin(BlockBehaviour.BlockStateBase.class)
abstract class HostSurfaceMixin {
    private VoxelShape dqj$pickShape(BlockGetter level,BlockPos pos){
        if(dev.rehan.passthrough.HostTerrainTarget.ignoringProxies()&&WorldBridge.isHostProxy(pos)
            &&((BlockState)(Object)this).is(Blocks.BARRIER)&&level instanceof Level world&&world.dimension()==Level.OVERWORLD)
            return net.minecraft.world.phys.shapes.Shapes.empty();
        return dqj$surface(level,pos);
    }
    private VoxelShape dqj$surface(BlockGetter level,BlockPos pos){
        if(!((BlockState)(Object)this).is(Blocks.BARRIER)||!(level instanceof Level world)||world.dimension()!=Level.OVERWORLD)return null;
        return WorldBridge.surfaceShape(pos);
    }
    @Inject(method="getCollisionShape(Lnet/minecraft/world/level/BlockGetter;Lnet/minecraft/core/BlockPos;Lnet/minecraft/world/phys/shapes/CollisionContext;)Lnet/minecraft/world/phys/shapes/VoxelShape;",at=@At("HEAD"),cancellable=true)
    private void dqj$collision(BlockGetter level,BlockPos pos,CollisionContext context,CallbackInfoReturnable<VoxelShape> ci){var shape=dqj$surface(level,pos);if(shape!=null)ci.setReturnValue(shape);}
    @Inject(method="getCollisionShape(Lnet/minecraft/world/level/BlockGetter;Lnet/minecraft/core/BlockPos;)Lnet/minecraft/world/phys/shapes/VoxelShape;",at=@At("HEAD"),cancellable=true)
    private void dqj$collisionSimple(BlockGetter level,BlockPos pos,CallbackInfoReturnable<VoxelShape> ci){var shape=dqj$surface(level,pos);if(shape!=null)ci.setReturnValue(shape);}
    @Inject(method="getShape(Lnet/minecraft/world/level/BlockGetter;Lnet/minecraft/core/BlockPos;Lnet/minecraft/world/phys/shapes/CollisionContext;)Lnet/minecraft/world/phys/shapes/VoxelShape;",at=@At("HEAD"),cancellable=true)
    private void dqj$selection(BlockGetter level,BlockPos pos,CollisionContext context,CallbackInfoReturnable<VoxelShape> ci){var shape=dqj$pickShape(level,pos);if(shape!=null)ci.setReturnValue(shape);}
    @Inject(method="getShape(Lnet/minecraft/world/level/BlockGetter;Lnet/minecraft/core/BlockPos;)Lnet/minecraft/world/phys/shapes/VoxelShape;",at=@At("HEAD"),cancellable=true)
    private void dqj$selectionSimple(BlockGetter level,BlockPos pos,CallbackInfoReturnable<VoxelShape> ci){var shape=dqj$pickShape(level,pos);if(shape!=null)ci.setReturnValue(shape);}
    @Inject(method="getInteractionShape",at=@At("HEAD"),cancellable=true)
    private void dqj$interaction(BlockGetter level,BlockPos pos,CallbackInfoReturnable<VoxelShape> ci){var shape=dqj$pickShape(level,pos);if(shape!=null)ci.setReturnValue(shape);}
    @Inject(method="isCollisionShapeFullBlock",at=@At("HEAD"),cancellable=true)
    private void dqj$full(BlockGetter level,BlockPos pos,CallbackInfoReturnable<Boolean> ci){var shape=dqj$surface(level,pos);if(shape!=null)ci.setReturnValue(false);}
}
