package dev.rehan.passthrough.client.mixin;

import com.llamalad7.mixinextras.sugar.Local;
import dev.rehan.passthrough.WorldBridge;
import net.minecraft.client.renderer.block.LiquidBlockRenderer;
import net.minecraft.core.BlockPos;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.ModifyArg;

/** Preview85 (user: lava/water from a bucket floats): a fluid cell resting on a host ground proxy is
 *  drawn down at that proxy's native ground height (proxy cell height - 1), so it lies on Wukong
 *  terrain. Fluid rules (flow, contact, damage) stay vanilla on the grid. */
@Mixin(LiquidBlockRenderer.class)
abstract class HostLiquidGroundMixin {
    @ModifyArg(method="tesselate",at=@At(value="INVOKE",
        target="Lnet/minecraft/client/renderer/block/LiquidBlockRenderer;vertex(Lcom/mojang/blaze3d/vertex/VertexConsumer;FFFFFFFFI)V"),index=2)
    private float dqj$ground(float y,@Local(argsOnly=true) BlockPos pos){
        double drop=WorldBridge.surfaceDrop(pos.below());
        return drop>0?(float)(y-drop):y;
    }
}
