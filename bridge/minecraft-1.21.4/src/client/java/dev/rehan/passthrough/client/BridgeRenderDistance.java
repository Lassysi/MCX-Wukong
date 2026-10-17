package dev.rehan.passthrough.client;

import dev.rehan.passthrough.Passthrough;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;

/** Preview77 (user: show farther blocks). The lab helper forces renderDistance 2 at startup;
 *  once the player is in a world the bridge raises it to -Ddqj.renderDistance (default 6). */
final class BridgeRenderDistance {
    private BridgeRenderDistance() {}
    private static boolean applied;
    static void register(){
        ClientTickEvents.END_CLIENT_TICK.register(mc->{
            if(applied||mc.level==null||mc.player==null)return;
            applied=true;
            int wanted=Math.max(2,Math.min(32,Integer.getInteger("dqj.renderDistance",6)));
            int current=mc.options.renderDistance().get();
            if(current<wanted){mc.options.renderDistance().set(wanted);mc.options.save();
                Passthrough.LOG.info("DQJ render distance {} -> {} chunks",current,wanted);}
        });
    }
}
