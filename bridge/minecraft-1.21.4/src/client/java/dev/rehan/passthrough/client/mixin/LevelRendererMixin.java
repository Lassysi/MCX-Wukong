package dev.rehan.passthrough.client.mixin;
import dev.rehan.passthrough.client.FrameExporter;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.NativeControl;
import com.mojang.blaze3d.vertex.PoseStack;
import com.mojang.blaze3d.vertex.VertexConsumer;
import net.minecraft.core.BlockPos;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.level.block.Blocks;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.client.renderer.LevelRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(LevelRenderer.class)
abstract class LevelRendererMixin {
    @Inject(method="addSkyPass",at=@At("HEAD"),cancellable=true)
    private void dqj$hostSky(CallbackInfo ci){if(Passthrough.minecraftAuthority){NativeControl.skyPassesSuppressed++;ci.cancel();}}
    @Inject(method="addCloudsPass",at=@At("HEAD"),cancellable=true)
    private void dqj$hostClouds(CallbackInfo ci){if(Passthrough.minecraftAuthority){NativeControl.cloudPassesSuppressed++;ci.cancel();}}
    @Inject(method="renderLevel",at=@At("TAIL"))
    private void dqj$world(CallbackInfo ci) { FrameExporter.world(); }
    @Inject(method="renderHitOutline",at=@At("HEAD"),cancellable=true)
    private void dqj$hideProxyOutline(PoseStack stack,VertexConsumer vertices,Entity entity,
        double x,double y,double z,BlockPos pos,BlockState block,int color,CallbackInfo ci) {
        if(Passthrough.minecraftAuthority && block.is(Blocks.BARRIER))ci.cancel();
    }
}
