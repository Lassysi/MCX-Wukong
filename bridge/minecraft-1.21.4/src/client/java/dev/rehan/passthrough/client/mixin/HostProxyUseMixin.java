package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.CombatBridge;
import net.minecraft.client.Minecraft;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Preview126 (user: next to a Boss right-click did nothing: no blocks, no fire): the enlarged hitboxes caught the crosshair, so
 *  use presses interacted with an invisible pig. Right-click now goes through proxies to the block behind; left-click unchanged. */
@Mixin(Minecraft.class)
abstract class HostProxyUseMixin {
    @org.spongepowered.asm.mixin.Unique private static int dqj$logs;
    @Inject(method="startUseItem",at=@At("HEAD"))
    private void dqj$useThroughProxy(CallbackInfo ci){
        var mc=(Minecraft)(Object)this;
        var before=mc.hitResult;var after=CombatBridge.useTarget(before,mc.player);
        if(after==before)return;
        mc.hitResult=after;
        if(dqj$logs++<50)dev.rehan.passthrough.Passthrough.LOG.info("DQJ place-diag use passed through an enemy hitbox -> {}",after.getType());
    }
}
