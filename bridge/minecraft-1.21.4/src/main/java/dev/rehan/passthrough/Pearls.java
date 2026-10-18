package dev.rehan.passthrough;

import java.util.HashMap;
import java.util.Locale;
import java.util.Map;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.projectile.ThrownEnderpearl;
import net.minecraft.world.phys.Vec3;

/** Preview107 (user: ender pearls can't teleport). MC only has Wukong's ground and walls rastered near Steve, so a pearl
 *  thrown further flew through the real terrain and fell out of the world. While bridged, every flying pearl sends its
 *  segment of this tick to the host ({"t":"pearl","id","a","b"}); the host traces it against the native world and replies
 *  "pearl_hit" with a point backed off the surface, where the thrower is teleported (vanilla rules otherwise: the pearl is
 *  consumed, fall distance reset; no pearl damage). The host is also asked to raster ground at the landing point. */
public final class Pearls {
    private Pearls() {}
    private static final Map<Integer,Vec3> last=new HashMap<>();
    static void tick(MinecraftServer server){
        if(!Passthrough.active){last.clear();return;}
        ServerLevel level=server.overworld();int sent=0;
        var seen=new java.util.HashSet<Integer>();
        for(var entity:level.getAllEntities()){
            if(!(entity instanceof ThrownEnderpearl pearl)||!(pearl.getOwner() instanceof ServerPlayer))continue;
            int id=pearl.getId();seen.add(id);
            Vec3 now=pearl.position(),before=last.put(id,now);
            if(before==null||before.distanceToSqr(now)<1e-4||sent++>=4)continue;
            Passthrough.events.accept(String.format(Locale.ROOT,"{\"t\":\"pearl\",\"id\":%d,\"a\":[%.3f,%.3f,%.3f],\"b\":[%.3f,%.3f,%.3f]}",
                id,before.x,before.y,before.z,now.x,now.y,now.z));
        }
        last.keySet().retainAll(seen);
    }
    public static void hit(int id,double x,double y,double z){
        var s=WorldBridge.server();if(s==null)return;
        s.execute(()->{
            var entity=s.overworld().getEntity(id);
            if(!(entity instanceof ThrownEnderpearl pearl)||pearl.isRemoved()||!(pearl.getOwner() instanceof ServerPlayer player))return;
            Passthrough.events.accept(String.format(Locale.ROOT,"{\"t\":\"ally_ground\",\"p\":[%.3f,%.3f,%.3f]}",x,y,z));
            player.teleportTo(x,y,z);player.resetFallDistance();pearl.discard();last.remove(id);
            Passthrough.LOG.info("DQJ pearl: teleported {} to {},{},{} (native surface hit)",player.getScoreboardName(),
                String.format(Locale.ROOT,"%.2f",x),String.format(Locale.ROOT,"%.2f",y),String.format(Locale.ROOT,"%.2f",z));
        });
    }
}
