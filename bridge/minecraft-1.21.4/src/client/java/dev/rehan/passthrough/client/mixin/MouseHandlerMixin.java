package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.NativeControl;
import net.minecraft.client.MouseHandler;
import net.minecraft.client.Minecraft;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.Redirect;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(MouseHandler.class)
abstract class MouseHandlerMixin {
    @Shadow private double accumulatedDX,accumulatedDY;
    @Redirect(method={"onMove","handleAccumulatedMovement"},at=@At(value="INVOKE",target="Lnet/minecraft/client/Minecraft;isWindowActive()Z"))
    private boolean dqj$bridgeGuiFocus(Minecraft minecraft){
        return minecraft.isWindowActive()||(Passthrough.minecraftAuthority&&NativeControl.transportPointer&&minecraft.screen!=null);
    }
    @Inject(method="onMove",at=@At("HEAD"),cancellable=true)
    private void dqj$oneGuiPointer(long window,double x,double y,CallbackInfo ci){
        if(Passthrough.minecraftAuthority&&Minecraft.getInstance().screen!=null&&!NativeControl.transportPointer)ci.cancel();
    }
    @Inject(method="onPress",at=@At("HEAD"),cancellable=true)
    private void dqj$oneGuiButton(long window,int button,int action,int mods,CallbackInfo ci){
        if(Passthrough.minecraftAuthority&&Minecraft.getInstance().screen!=null&&!NativeControl.transportButton)ci.cancel();
    }
    @Inject(method="grabMouse",at=@At("HEAD"),cancellable=true)
    private void dqj$physicalCursor(CallbackInfo ci){if(Passthrough.active)ci.cancel();}
    @Inject(method="turnPlayer",at=@At("HEAD"),cancellable=true)
    private void dqj$singleInputOwner(CallbackInfo ci) {
        if(Passthrough.minecraftAuthority && !NativeControl.transportTurn){accumulatedDX=accumulatedDY=0;ci.cancel();}
    }
    @Inject(method="onScroll",at=@At("HEAD"),cancellable=true)
    private void dqj$singleScrollOwner(CallbackInfo ci){if(Passthrough.minecraftAuthority&&!NativeControl.transportScroll)ci.cancel();}
    @Inject(method="isMouseGrabbed",at=@At("HEAD"),cancellable=true)
    private void dqj$hostGameplayCursor(CallbackInfoReturnable<Boolean> cir) {
        // Vanilla continuous mining requires a grabbed gameplay mouse. The host
        // owns the physical cursor; report logical capture without grabbing Windows.
        if(Passthrough.minecraftAuthority && Minecraft.getInstance().screen==null) cir.setReturnValue(true);
    }
}
