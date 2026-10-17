package dev.rehan.passthrough.client;

import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.mixin.ItemInHandAccess;
import net.minecraft.client.Minecraft;

/** Preview85 (BUG-10: after respawn/teleport/item switches the empty first-person hand sometimes stayed
 *  invisible until F5 cycling or a wait): while MC owns the session in first person with an empty main
 *  hand and a full attack cooldown, a hand height stuck below 0.1 for 10 ticks is raised. Each case is
 *  logged once with the state that held it down. */
final class HandRecovery {
    private static int lowTicks;private static boolean logged;
    private HandRecovery() {}
    static void tick(Minecraft mc){
        var p=mc.player;
        if(p==null||!Passthrough.minecraftAuthority||!mc.options.getCameraType().isFirstPerson()||mc.screen!=null||!p.getMainHandItem().isEmpty()){lowTicks=0;return;}
        var hand=(ItemInHandAccess)mc.getEntityRenderDispatcher().getItemInHandRenderer();
        float height=hand.dqj$mainHeight(),strength=p.getAttackStrengthScale(1f);
        if(height>=.1f||strength<.99f||p.isHandsBusy()){lowTicks=0;logged=false;return;}
        if(++lowTicks<10)return;
        if(!logged){logged=true;Passthrough.LOG.info("DQJ hand-recovery empty hand stuck height={} strength={} hideGui={} invisible={} spectator={} handsBusy={}; raised",
            height,strength,mc.options.hideGui,p.isInvisible(),p.isSpectator(),p.isHandsBusy());}
        hand.dqj$setMainHeight(1f);hand.dqj$setOldMainHeight(1f);lowTicks=0;
    }
}
