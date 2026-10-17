package dev.rehan.passthrough.client.mixin;

import com.llamalad7.mixinextras.injector.wrapoperation.Operation;
import com.llamalad7.mixinextras.injector.wrapoperation.WrapOperation;
import dev.rehan.passthrough.HostBlockAnchors;
import net.fabricmc.fabric.impl.client.indigo.renderer.render.BlockRenderContext;
import net.minecraft.client.Minecraft;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;

@Mixin(value=BlockRenderContext.class,remap=false)
abstract class HostIndigoBlockMixin {
    @WrapOperation(method="render",remap=true,at=@At(value="INVOKE",target="Lnet/minecraft/world/level/block/state/BlockState;getOffset(Lnet/minecraft/core/BlockPos;)Lnet/minecraft/world/phys/Vec3;",remap=true))
    private Vec3 dqj$offset(BlockState state,BlockPos pos,Operation<Vec3> original){
        Vec3 base=original.call(state,pos);var level=Minecraft.getInstance().level;
        return level!=null&&HostBlockAnchors.world(level)?base.add(HostBlockAnchors.offset(state,pos)):base;
    }
}
