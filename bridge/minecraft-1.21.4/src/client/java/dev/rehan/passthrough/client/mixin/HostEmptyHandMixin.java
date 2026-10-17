package dev.rehan.passthrough.client.mixin;

import dev.rehan.passthrough.Passthrough;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.ItemInHandRenderer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** Preview86 (BUG-10: empty first-person hand invisible after item switch/respawn/teleport): vanilla raises the
 *  main-hand height toward attackStrength^3, and Better Combat keeps that strength below 1 for a while, so the
 *  bare arm stayed below the screen. While MC owns the session an empty main hand is always fully raised. */
@Mixin(ItemInHandRenderer.class)
abstract class HostEmptyHandMixin {
    @Shadow private float mainHandHeight;
    @Shadow private net.minecraft.world.item.ItemStack mainHandItem;
    @Shadow private float oMainHandHeight;
    private static boolean dqj$logged;
    @Inject(method="tick",at=@At("TAIL"))
    private void dqj$emptyHand(CallbackInfo ci){
        var player=Minecraft.getInstance().player;
        if(player==null||!Passthrough.minecraftAuthority||player.isHandsBusy())return;
        if(mainHandHeight<.5f&&!dqj$logged){dqj$logged=true;
            Passthrough.LOG.info("DQJ hand-recovery empty hand height={} strength={}; held fully raised while bridged",mainHandHeight,player.getAttackStrengthScale(1f));}
        // Preview87 (Live86: the previous block stayed in the hand): vanilla only swaps the shown item after
        // the lowering animation passes 0.1, which the held height never allowed. Show the empty hand at once.
        // Preview92 (live: switched items sometimes stayed hidden too): every held item, not only the empty hand,
        // is shown at once at full height while bridged (no lowering/raising equip animation).
        mainHandItem=player.getMainHandItem();mainHandHeight=1f;oMainHandHeight=1f;
    }
}
