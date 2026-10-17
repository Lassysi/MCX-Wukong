package dev.rehan.passthrough.client.mixin;

import com.mojang.blaze3d.platform.InputConstants;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.NativeControl;
import net.minecraft.client.gui.screens.inventory.AbstractContainerScreen;
import org.lwjgl.glfw.GLFW;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Redirect;

/** Vanilla container clicks read GLFW directly instead of Screen.hasShiftDown. */
@Mixin(AbstractContainerScreen.class)
abstract class HostContainerModifiersMixin {
    @Redirect(method="mouseClicked",at=@At(value="INVOKE",target="Lcom/mojang/blaze3d/platform/InputConstants;isKeyDown(JI)Z"))
    private boolean dqj$shift(long window,int key){
        if(Passthrough.minecraftAuthority&&(key==GLFW.GLFW_KEY_LEFT_SHIFT||key==GLFW.GLFW_KEY_RIGHT_SHIFT))
            return NativeControl.shiftHeld;
        return InputConstants.isKeyDown(window,key);
    }
}
