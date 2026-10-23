package dev.rehan.passthrough.client;

import dev.rehan.passthrough.Passthrough;
import net.minecraft.client.Minecraft;

/** Preview127: movement keys held under MC control while Steve does not move for 2 s -> StuckRecovery reports and clears known traps.
 *  Backs off 10 s after each check, so pushing against a wall only logs occasionally. */
final class StuckWatchdog {
    private StuckWatchdog() {}
    private static double lastX,lastZ;private static int still,backoff;
    static void tick(Minecraft mc){
        var p=mc.player;if(backoff>0)backoff--;
        var o=mc.options;
        boolean trying=p!=null&&p.isAlive()&&mc.screen==null&&Passthrough.minecraftAuthority
            &&(o.keyUp.isDown()||o.keyDown.isDown()||o.keyLeft.isDown()||o.keyRight.isDown());
        if(p==null)return;
        double moved=Math.hypot(p.getX()-lastX,p.getZ()-lastZ);lastX=p.getX();lastZ=p.getZ();
        if(!trying||moved>.01){still=0;return;}
        if(++still<40||backoff>0)return;
        still=0;backoff=200;
        var server=mc.getSingleplayerServer();if(server==null)return;
        var id=p.getUUID();server.execute(()->dev.rehan.passthrough.StuckRecovery.check(server,id));
    }
}
