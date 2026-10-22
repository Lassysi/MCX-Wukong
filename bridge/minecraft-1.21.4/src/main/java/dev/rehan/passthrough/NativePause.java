package dev.rehan.passthrough;

import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import net.fabricmc.fabric.api.entity.event.v1.ServerLivingEntityEvents;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerLifecycleEvents;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerTickEvents;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.phys.Vec3;

/** Freeze only the idle MC avatar during an explicit F8 native lease. Native health/mana remain native. */
public final class NativePause {
    private NativePause() {}
    private record Saved(Vec3 at,boolean noGravity,int food,float saturation) {}
    private static final ConcurrentHashMap<UUID,Saved> saved=new ConcurrentHashMap<>();
    private static volatile long expires;
    public static void register(){
        ServerTickEvents.END_SERVER_TICK.register(NativePause::tick);
        ServerLifecycleEvents.SERVER_STOPPING.register(NativePause::clear);
        // Preview125 (MC log 02:19: an ender pearl's own 5 HP landing damage killed Steve at 4.2 HP): pearls are the bridge's
        // movement tool, so their self-damage is dropped while bridged.
        ServerLivingEntityEvents.ALLOW_DAMAGE.register((entity,source,amount)->!held(entity)
            &&!(Passthrough.active&&entity instanceof ServerPlayer&&source.is(net.minecraft.world.damagesource.DamageTypes.ENDER_PEARL)));
    }
    public static boolean held(Entity e){return System.nanoTime()<expires&&saved.containsKey(e.getUUID());}
    public static Vec3 position(UUID id){var s=saved.get(id);return s!=null&&System.nanoTime()<expires?s.at():null;}
    public static void renew(boolean hold){
        var server=WorldBridge.server();if(server==null)return;
        server.execute(()->{if(hold){expires=System.nanoTime()+3_000_000_000L;tick(server);}else clear(server);});
    }
    private static void food(ServerPlayer p,Saved s){
        // 1.21.4 FoodData has no public exhaustion accessor; food and saturation are restored every tick, so a frozen avatar never loses food.
        p.getFoodData().setFoodLevel(s.food());p.getFoodData().setSaturation(s.saturation());
    }
    private static void tick(MinecraftServer server){
        if(System.nanoTime()>=expires){if(!saved.isEmpty())clear(server);return;}
        for(var p:server.getPlayerList().getPlayers()){
            var s=saved.computeIfAbsent(p.getUUID(),id->new Saved(p.position(),p.isNoGravity(),p.getFoodData().getFoodLevel(),p.getFoodData().getSaturationLevel()));
            p.setNoGravity(true);p.setDeltaMovement(Vec3.ZERO);p.fallDistance=0;
            if(p.position().distanceToSqr(s.at())>.000001)p.teleportTo(s.at().x,s.at().y,s.at().z);
            food(p,s);
        }
    }
    public static void clear(MinecraftServer server){
        expires=0;
        for(var p:server.getPlayerList().getPlayers()){
            var s=saved.get(p.getUUID());if(s==null)continue;
            p.setNoGravity(s.noGravity());p.setDeltaMovement(Vec3.ZERO);p.fallDistance=0;food(p,s);
        }
        saved.clear();
    }
}
