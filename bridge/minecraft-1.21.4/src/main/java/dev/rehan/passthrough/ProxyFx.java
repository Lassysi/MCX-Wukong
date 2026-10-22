package dev.rehan.passthrough;

import java.util.HashMap;
import java.util.Locale;
import java.util.Map;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.effect.MobEffects;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.level.block.Blocks;

/** Potions & items stage D1 (POTIONS-ITEMS-PLAN.md): MC applies potion effects, cobweb, powder snow and knockback to the
 *  invisible proxies of Wukong enemies, but only damage reached the real monster. Every 5 ticks this reports each proxy's
 *  slowness/weakness/levitation levels, freeze/cobweb/soul-sand contact and any knockback impulse to the host
 *  ({"t":"proxy_fx","e":[...]}), which applies native time dilation, damage scaling and launches. Only changed entries are
 *  sent; an entry with every field neutral tells the host to restore that monster. Player flags go as "player_fx". */
public final class ProxyFx {
    private ProxyFx() {}
    private static final Map<String,String> last=new HashMap<>();
    private static Boolean lastFireRes;
    private static Boolean lastGuard;
    /** A bounded heartbeat lets the native guard expire safely if the connection stops. */
    public static void sendPlayerFlags(net.minecraft.server.level.ServerPlayer p){
        boolean fireRes=p.hasEffect(MobEffects.FIRE_RESISTANCE);int ticks=GoldenApple.remainingTicks(p);boolean guard=ticks>0;
        if(lastFireRes==null||lastFireRes!=fireRes||lastGuard==null||lastGuard!=guard||guard&&tick%20==0){
            lastFireRes=fireRes;lastGuard=guard;
            Passthrough.events.accept("{\"t\":\"player_fx\",\"fireRes\":"+fireRes+",\"appleGuardTicks\":"+ticks+"}");
        }
    }
    private static int tick;
    /** Strongest knockback impulse per proxy since the last report (CombatBridge resets proxy velocity every frame). */
    private static final Map<String,net.minecraft.world.phys.Vec3> knock=new HashMap<>();
    static void capture(String id,net.minecraft.world.phys.Vec3 v){
        id=MultipartTargets.parentId(id);
        if(v.horizontalDistanceSqr()<.0225&&v.y<=.25)return;
        var old=knock.get(id);if(old==null||v.lengthSqr()>old.lengthSqr())knock.put(id,v);
    }

    /** Live112 polish (user): boats and minecarts capture small monsters (not bottom-health-bar enemies); big enemies also
     *  break a fishing line. Preview124 (user: finish it; capture as wide as reasonable, monsters wander): an empty boat or
     *  minecart within CaptureRange blocks of a monster's hitbox pulls it in, and the capture holds until the player breaks
     *  the vehicle (only the player can: CaptureVehicleGuardMixin) or the monster dies. A 10 s cooldown follows a release. */
    private record Seat(net.minecraft.world.entity.Entity vehicle,int since) {}
    private static final Map<java.util.UUID,Seat> seats=new HashMap<>();
    private static final Map<java.util.UUID,Integer> cooldown=new HashMap<>();
    private static final int CooldownTicks=200;
    static final double CaptureRange=4,CaptureRangeY=2;
    private static int refusedLogged;
    private static void bar(MinecraftServer s,String text){for(var p:s.getPlayerList().getPlayers())p.displayClientMessage(net.minecraft.network.chat.Component.literal(text),true);}
    private static boolean vehicle(net.minecraft.world.entity.Entity e){return e instanceof net.minecraft.world.entity.vehicle.AbstractBoat||e instanceof net.minecraft.world.entity.vehicle.AbstractMinecart;}
    /** Server-side mount gate also covers vanilla automatic boarding during the cooldown. */
    public static boolean mayBoard(net.minecraft.world.entity.Entity e,net.minecraft.world.entity.Entity v){
        if(e.level().isClientSide)return true;
        if(!CombatBridge.isProxy(e)||!vehicle(v))return true;
        Integer free=cooldown.get(e.getUUID());
        return !CombatBridge.big(e)&&(free==null||tick-free>=CooldownTicks);
    }
    static void clear(){seats.clear();cooldown.clear();knock.clear();last.clear();lastFireRes=null;lastGuard=null;}
    private static void release(String id,net.minecraft.world.entity.animal.Pig pig,String reason){
        seats.remove(pig.getUUID());cooldown.put(pig.getUUID(),tick);
        if(vehicle(pig.getVehicle()))pig.stopRiding();
        Passthrough.LOG.info("DQJ capture: enemy {} released ({})",id,reason);
    }
    private static boolean capture(MinecraftServer server,net.minecraft.server.level.ServerLevel level,String id,net.minecraft.world.entity.animal.Pig pig){
        var uuid=pig.getUUID();boolean big=CombatBridge.big(pig);
        for(var hook:level.getEntitiesOfClass(net.minecraft.world.entity.projectile.FishingHook.class,pig.getBoundingBox().inflate(3)))
            if(hook.getHookedIn()==pig&&big){hook.discard();bar(server,"Too big to reel in");Passthrough.LOG.info("DQJ capture: fishing line broke on big enemy {}",id);}
        Seat seat=seats.get(uuid);
        if(seat!=null){
            var v=seat.vehicle();
            if(big||!v.isAlive()){
                release(id,pig,big?"big enemy":"vehicle broken");return false;
            }
            if(pig.getVehicle()!=v){
                // A transient dismount must not erase ownership.
                if(!v.getPassengers().isEmpty()||!pig.startRiding(v,true)){
                    release(id,pig,"seat unavailable");return false;
                }
                Passthrough.LOG.info("DQJ capture: enemy {} restored to its seat",id);
            }
            CombatBridge.seated(pig);
            return true;
        }
        if(!mayBoard(pig,pig.getVehicle())&&vehicle(pig.getVehicle())){pig.stopRiding();return false;}
        if(big)return false;
        Integer free=cooldown.get(uuid);if(free!=null&&tick-free<CooldownTicks){if(vehicle(pig.getVehicle()))pig.stopRiding();return false;}
        if(vehicle(pig.getVehicle())){
            seats.put(uuid,new Seat(pig.getVehicle(),tick));CombatBridge.seated(pig);
            bar(server,"Monster captured!");Passthrough.LOG.info("DQJ capture: enemy {} seated in {}",id,pig.getVehicle().getType().getDescriptionId());return true;
        }
        if(pig.isPassenger())return false;
        // Live113 (user: a monster next to a boat was not caught): Wukong monsters don't step onto boats like MC mobs, so an empty
        // boat/minecart near the hitbox pulls the monster in (Preview124: 4 blocks sideways, 2 up/down; nearest vehicle first).
        var near=level.getEntitiesOfClass(net.minecraft.world.entity.vehicle.VehicleEntity.class,pig.getBoundingBox().inflate(CaptureRange,CaptureRangeY,CaptureRange));
        near.sort(java.util.Comparator.comparingDouble(v->v.distanceToSqr(pig)));
        for(var v:near)
            if(vehicle(v)&&v.isAlive()&&v.getPassengers().isEmpty()&&seats.values().stream().noneMatch(s->s.vehicle()==v)){
                if(pig.startRiding(v,true)){seats.put(uuid,new Seat(v,tick));CombatBridge.seated(pig);bar(server,"Monster captured!");Passthrough.LOG.info("DQJ capture: enemy {} seated in {}",id,v.getType().getDescriptionId());return true;}
                if(refusedLogged++<10)Passthrough.LOG.info("DQJ capture: enemy {} could not board {} (vanilla refused)",id,v.getType().getDescriptionId());
            }
        return false;
    }

    private static int amp(LivingEntity e,net.minecraft.core.Holder<net.minecraft.world.effect.MobEffect> effect){
        var i=e.getEffect(effect);return i==null?-1:i.getAmplifier();
    }
    static void tick(MinecraftServer server){
        if(!Passthrough.active){if(!last.isEmpty()){last.clear();}lastFireRes=null;return;}
        ++tick;if(tick%5!=0&&knock.isEmpty())return;
        MultipartTargets.mergeEffects();
        var level=server.overworld();var out=new StringBuilder();int n=0;
        var seen=new java.util.HashSet<String>();
        for(var entry:CombatBridge.proxies().entrySet()){
            var pig=entry.getValue();if(pig==null||pig.isRemoved()||MultipartTargets.isPart(pig))continue;
            String id=entry.getKey();seen.add(id);
            var feet=pig.blockPosition();var at=level.getBlockState(feet);var below=level.getBlockState(feet.below());
            int slow=amp(pig,MobEffects.MOVEMENT_SLOWDOWN),weak=amp(pig,MobEffects.WEAKNESS),lev=amp(pig,MobEffects.LEVITATION);
            boolean freeze=pig.getTicksFrozen()>0&&(at.is(Blocks.POWDER_SNOW)||pig.isFullyFrozen());
            boolean web=at.is(Blocks.COBWEB)||level.getBlockState(feet.above()).is(Blocks.COBWEB);
            boolean soul=below.is(Blocks.SOUL_SAND)||below.is(Blocks.SOUL_SOIL)||below.is(Blocks.HONEY_BLOCK)||at.is(Blocks.SOUL_SAND)||at.is(Blocks.HONEY_BLOCK);
            // Report one aggregate status row. A neutral tail must never cancel an affected head.
            for(var partEntry:CombatBridge.proxies().entrySet()){
                if(partEntry.getKey().equals(id)||!MultipartTargets.parentId(partEntry.getKey()).equals(id))continue;
                var part=partEntry.getValue();if(part.isRemoved())continue;
                var pf=part.blockPosition();var pa=level.getBlockState(pf);var pb=level.getBlockState(pf.below());
                web|=pa.is(Blocks.COBWEB)||level.getBlockState(pf.above()).is(Blocks.COBWEB);
                soul|=pb.is(Blocks.SOUL_SAND)||pb.is(Blocks.SOUL_SOIL)||pb.is(Blocks.HONEY_BLOCK)||pa.is(Blocks.SOUL_SAND)||pa.is(Blocks.HONEY_BLOCK);
                freeze|=part.getTicksFrozen()>0&&(pa.is(Blocks.POWDER_SNOW)||part.isFullyFrozen());
            }
            boolean seat=capture(server,level,id,pig);
            var v=knock.remove(id);
            String kb=v==null?null:String.format(Locale.ROOT,"[%.3f,%.3f,%.3f]",v.x,v.y,v.z);
            String sig=slow+","+weak+","+lev+","+freeze+","+web+","+soul+","+seat;
            // Seated positions are a short heartbeat: native actors must follow the seat, not only freeze beside it.
            if(kb==null&&(tick%5!=0||!seat&&sig.equals(last.get(id))))continue;
            boolean neutral=slow<0&&weak<0&&lev<0&&!freeze&&!web&&!soul&&!seat;
            if(neutral&&kb==null&&!last.containsKey(id))continue;
            if(neutral)last.remove(id);else last.put(id,sig);
            if(n++>0)out.append(',');
            out.append("{\"id\":\"").append(id).append("\",\"slow\":").append(slow).append(",\"weak\":").append(weak).append(",\"lev\":").append(lev)
               .append(",\"freeze\":").append(freeze).append(",\"web\":").append(web).append(",\"soul\":").append(soul).append(",\"seat\":").append(seat);
            if(kb!=null)out.append(",\"kb\":").append(kb);
            if(seat)out.append(",\"seat_p\":").append(String.format(Locale.ROOT,"[%.4f,%.4f,%.4f]",pig.getX(),pig.getY(),pig.getZ()));
            out.append('}');
            if(n>=48)break;
        }
        last.keySet().retainAll(seen);knock.clear();
        var present=new java.util.HashSet<java.util.UUID>();for(var p:CombatBridge.proxies().values())present.add(p.getUUID());
        seats.keySet().retainAll(present);cooldown.keySet().retainAll(present);
        if(n>0)Passthrough.events.accept("{\"t\":\"proxy_fx\",\"e\":["+out+"]}");
        var players=server.getPlayerList().getPlayers();
        if(!players.isEmpty()){
            sendPlayerFlags(players.get(0));
        }
    }
}
