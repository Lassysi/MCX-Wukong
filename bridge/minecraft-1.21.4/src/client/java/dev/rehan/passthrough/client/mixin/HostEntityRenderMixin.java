package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.CombatBridge;
import dev.rehan.passthrough.client.NativeControl;
import net.minecraft.client.renderer.entity.EntityRenderDispatcher;
import net.minecraft.client.renderer.MultiBufferSource;
import com.mojang.blaze3d.vertex.PoseStack;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.LivingEntity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Hide bridge-owned hit targets, including hurt flashes/shadows and mod renderers. */
@Mixin(EntityRenderDispatcher.class)
abstract class HostEntityRenderMixin {
    @Inject(method="render(Lnet/minecraft/world/entity/Entity;DDDFLcom/mojang/blaze3d/vertex/PoseStack;Lnet/minecraft/client/renderer/MultiBufferSource;I)V",at=@At("HEAD"),cancellable=true)
    private void dqj$proxy(Entity entity,double x,double y,double z,float partial,PoseStack stack,MultiBufferSource buffers,int light,CallbackInfo ci){
        if(CombatBridge.isProxy(entity)){NativeControl.proxyRendersSuppressed++;ci.cancel();}
    }
}
