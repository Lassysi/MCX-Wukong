package dev.rehan.passthrough;

import java.util.ArrayList;
import java.util.UUID;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;

/** Preview127 (user: after dying in the Erlang Shen gauntlet/rematch arena and respawning, Steve could not move for about 20 s; Live126 01:29:19-01:29:41
 *  shows no movement and no terrain request at all, so no single log line names the cause). The client watchdog calls this when
 *  movement keys are held under MC control and Steve has not moved for 2 s. Every known trap is reported, and the ones that can
 *  only be stale while MC has control are cleared: an F8/idle hold, no-gravity left over from a hold, Steve inside a block. */
public final class StuckRecovery {
    private StuckRecovery() {}
    private static int logs;
    public static String check(MinecraftServer s,UUID id){
        ServerPlayer p=s.getPlayerList().getPlayer(id);if(p==null||!p.isAlive())return "";
        var reasons=new ArrayList<String>();var fixes=new ArrayList<String>();
        if(NativePause.held(p)){reasons.add("F8/idle hold active");NativePause.clear(s);fixes.add("hold cleared");}
        if(Passthrough.active&&WorldBridge.isPreparing())reasons.add("ground handshake still preparing");
        if(Passthrough.active&&!WorldBridge.groundCommitted)reasons.add("ground not committed");
        if(TerrainLease.waiting)reasons.add("waiting for host terrain (epoch "+WorldBridge.groundEpoch+")");
        if(p.isNoGravity()&&!p.getAbilities().flying){reasons.add("no-gravity left on");p.setNoGravity(false);fixes.add("gravity restored");}
        var level=p.serverLevel();
        if(!level.noCollision(p,p.getBoundingBox().deflate(1e-3))){
            reasons.add("inside a block");
            for(int i=1;i<=20;i++){double dy=i*.1;
                if(level.noCollision(p,p.getBoundingBox().move(0,dy,0).deflate(1e-3))){p.teleportTo(p.getX(),p.getY()+dy,p.getZ());fixes.add(String.format("lifted %.1f m out of the block",dy));break;}
            }
        }
        String summary="reasons="+(reasons.isEmpty()?"none found (wall or edge?)":String.join(", ",reasons))+" fixes="+(fixes.isEmpty()?"none":String.join(", ",fixes));
        if(logs++<40)Passthrough.LOG.info("DQJ stuck-watchdog: movement keys held 2 s without moving at {} {} {}; {} horizontalCollision={} onGround={} mode={}",
            String.format("%.2f",p.getX()),String.format("%.2f",p.getY()),String.format("%.2f",p.getZ()),summary,p.horizontalCollision,p.onGround(),p.gameMode.getGameModeForPlayer());
        // Nothing fixable here and not pushing against a wall: ask the host for a fresh ground handshake (an Esc round trip did that in Live126).
        if(fixes.isEmpty()&&!p.horizontalCollision&&Passthrough.active&&Passthrough.minecraftAuthority){
            var out=new com.google.gson.JsonObject();out.addProperty("t","stuck");out.addProperty("why",summary);Passthrough.events.accept(out.toString());
        }
        return summary;
    }
}
