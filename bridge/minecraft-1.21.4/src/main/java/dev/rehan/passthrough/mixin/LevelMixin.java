package dev.rehan.passthrough.mixin;
import dev.rehan.passthrough.WorldBridge;
import net.minecraft.core.BlockPos;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.block.state.BlockState;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(Level.class)
abstract class LevelMixin {
    @Inject(method="setBlock(Lnet/minecraft/core/BlockPos;Lnet/minecraft/world/level/block/state/BlockState;II)Z",at=@At("RETURN"))
    private void dqj$changed(BlockPos pos,BlockState state,int flags,int depth,CallbackInfoReturnable<Boolean> ci) {
        if(ci.getReturnValue()&&(Object)this instanceof ServerLevel level) WorldBridge.changed(level,pos,state);
    }
}
