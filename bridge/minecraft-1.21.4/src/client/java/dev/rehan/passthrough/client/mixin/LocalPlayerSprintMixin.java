package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.client.NativeControl;
import net.minecraft.client.player.LocalPlayer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(LocalPlayer.class)
abstract class LocalPlayerSprintMixin {
    @Inject(method="aiStep",at=@At("TAIL"))
    private void dqj$releaseCancelledSprint(CallbackInfo ci){NativeControl.afterMovementTick((LocalPlayer)(Object)this);}
}
