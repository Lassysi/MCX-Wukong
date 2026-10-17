package dev.rehan.passthrough.client.mixin;
import dev.rehan.passthrough.client.FrameExporter;
import dev.rehan.passthrough.client.HostState;
import dev.rehan.passthrough.client.NativeControl;
import net.minecraft.client.renderer.GameRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.Redirect;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(GameRenderer.class)
abstract class GameRendererMixin {
    @Redirect(method="pick(Lnet/minecraft/world/entity/Entity;DDF)Lnet/minecraft/world/phys/HitResult;",at=@At(value="INVOKE",target="Lnet/minecraft/world/entity/Entity;pick(DFZ)Lnet/minecraft/world/phys/HitResult;"))
    private net.minecraft.world.phys.HitResult dqj$nativeSurface(net.minecraft.world.entity.Entity entity,double range,float partial,boolean fluids){
        return dev.rehan.passthrough.HostTerrainTarget.pick(entity,range,partial,fluids);
    }
    @Inject(method="renderLevel",at=@At("HEAD"))
    private void dqj$input(CallbackInfo ci) { HostState.refresh();NativeControl.beforeCamera(); }
    @Inject(method="render",at=@At("TAIL"))
    private void dqj$overlay(CallbackInfo ci) { FrameExporter.overlay(); }
    @Inject(method="renderItemInHand",at=@At("HEAD"))
    private void dqj$handBegin(CallbackInfo ci) { FrameExporter.beginHand(); }
    @Inject(method="renderItemInHand",at=@At("RETURN"))
    private void dqj$handEnd(CallbackInfo ci) { FrameExporter.endHand(); }
}
