package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.NativeControl;
import net.minecraft.client.gui.screens.Screen;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** The host is focused; GLFW in the background MC window cannot see its modifiers. */
@Mixin(Screen.class)
abstract class HostGuiModifiersMixin {
    @Inject(method="hasShiftDown",at=@At("HEAD"),cancellable=true)
    private static void dqj$shift(CallbackInfoReturnable<Boolean> cir){
        if(Passthrough.minecraftAuthority)cir.setReturnValue(NativeControl.shiftHeld);
    }
    @Inject(method="hasControlDown",at=@At("HEAD"),cancellable=true)
    private static void dqj$control(CallbackInfoReturnable<Boolean> cir){
        if(Passthrough.minecraftAuthority)cir.setReturnValue(NativeControl.controlHeld);
    }
}
