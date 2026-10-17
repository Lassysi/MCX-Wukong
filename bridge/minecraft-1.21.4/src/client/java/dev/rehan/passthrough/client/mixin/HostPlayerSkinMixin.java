package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.client.BridgePlayerSkin;
import net.minecraft.client.Minecraft;
import net.minecraft.client.player.AbstractClientPlayer;
import net.minecraft.client.resources.PlayerSkin;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** The local MC body, inventory portrait and first-person arms share the cutscene skin. */
@Mixin(AbstractClientPlayer.class)
abstract class HostPlayerSkinMixin {
    @Inject(method="getSkin",at=@At("RETURN"),cancellable=true)
    private void dqj$localSkin(CallbackInfoReturnable<PlayerSkin> cir){
        var local=Minecraft.getInstance().player;
        if(local!=null&&local.getUUID().equals(((AbstractClientPlayer)(Object)this).getUUID()))
            cir.setReturnValue(BridgePlayerSkin.apply(cir.getReturnValue()));
    }
}
