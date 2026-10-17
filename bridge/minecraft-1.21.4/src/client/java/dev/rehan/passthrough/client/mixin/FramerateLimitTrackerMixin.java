package dev.rehan.passthrough.client.mixin;

import com.mojang.blaze3d.platform.FramerateLimitTracker;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.HostState;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** A minimized MC window is still the renderer while the host owns the display. */
@Mixin(FramerateLimitTracker.class)
abstract class FramerateLimitTrackerMixin {
    @Shadow private int framerateLimit;

    @Inject(method="getFramerateLimit", at=@At("HEAD"), cancellable=true)
    private void dqj$keepBridgeRendering(CallbackInfoReturnable<Integer> cir) {
        // Check the heartbeat here too: a paused client may not run client ticks.
        HostState.refresh();
        if (Passthrough.active) cir.setReturnValue(framerateLimit);
    }
}
