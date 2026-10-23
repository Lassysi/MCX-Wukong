package dev.rehan.passthrough;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import java.util.*;
import java.util.concurrent.ConcurrentHashMap;
import net.fabricmc.fabric.api.entity.event.v1.ServerLivingEntityEvents;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerEntityEvents;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.*;
import net.minecraft.world.entity.ai.attributes.Attributes;
import net.minecraft.world.entity.animal.Pig;
import net.minecraft.world.damagesource.DamageSource;

/** Native MC entities receive native/mod damage; only outcomes cross the host boundary. */
public final class CombatBridge {
    private static final Map<String,Pig> targets=new LinkedHashMap<>();
    private static final Map<UUID,String> ids=new ConcurrentHashMap<>();
    private static final Map<UUID,float[]> sizes=new ConcurrentHashMap<>();
    /** Live112: host enemy category for Smite/Bane ("undead", "arthropod" or ""). */
    private static final Map<UUID,String> kinds=new ConcurrentHashMap<>();
    /** Live112 polish (user): enemies with a bottom health bar (host hpMax >= 1000) can't be captured or reeled in. */
    private static final Set<UUID> big=ConcurrentHashMap.newKeySet();
    static boolean big(Entity e){return big.contains(e.getUUID());}
    private static final Set<UUID> knownProxyIds=ConcurrentHashMap.newKeySet();
    /** C1: latest proxy feet positions, read by the client thread (same JVM) to snap proxies. */
    private static final Map<UUID,double[]> positions=new ConcurrentHashMap<>();
    private static long diagSecond;private static int diagLines;
    private static final Map<Long,Delta> pending=new LinkedHashMap<>();
    private static final Map<UUID,Float> observed=new HashMap<>();
    private static MinecraftServer server;
    private static String session="";
    private static long sequence,ack,received,lastHostHealth;
    private static boolean applying;
    private record Delta(String id,float amount) {}
    private CombatBridge() {}
    public static void attach(MinecraftServer s){
        server=s;
        // LIFE-01 (user 2026-10-16): Wukong-style death keeps items, so the bridge world keeps inventory.
        s.execute(()->{
            var rule=s.getGameRules().getRule(net.minecraft.world.level.GameRules.RULE_KEEPINVENTORY);
            if(!rule.get()){rule.set(true,s);Passthrough.LOG.info("DQJ life: gamerule keepInventory set true (MC death keeps items)");}
        });
    }
    public static void register(){
        ServerEntityEvents.ENTITY_LOAD.register((entity,level)->{
            // A crash may have saved an invisible bridge-owned proxy. Never remove
            // ordinary MC entities; current proxies register ownership before spawn.
            if(entity.getTags().contains("dqj.host_proxy") && !ids.containsKey(entity.getUUID()))entity.discard();
        });
        ServerLivingEntityEvents.ALLOW_DEATH.register((entity,source,amount)->{
            // LIFE-01: the MC player may die (MC death screen -> Respawn -> Wukong native rebirth);
            // host proxies stay held so Wukong owns enemy death.
            if(tracked(entity)&&!(entity instanceof ServerPlayer)) {entity.setHealth(.001f);return false;}
            return true;
        });
    }
    public static boolean tracked(LivingEntity entity){
        return !applying && server!=null && !session.isEmpty() && Passthrough.minecraftAuthority
            && (ids.containsKey(entity.getUUID()) || entity instanceof ServerPlayer);
    }
    public static EntityDimensions dimensions(LivingEntity entity){
        var size=sizes.get(entity.getUUID());
        return size==null?null:EntityDimensions.scalable(size[0],size[1]);
    }
    public static boolean isProxy(Entity entity){return knownProxyIds.contains(entity.getUUID());}
    /** Preview126 (Live125 21:34: 40 of 59 right-clicks with TNT hit an invisible Boss hitbox): a use press aimed at a proxy
     *  goes to the block behind it (or the air), exactly as if the proxy were not there. Attacks still target proxies. */
    public static net.minecraft.world.phys.HitResult useTarget(net.minecraft.world.phys.HitResult hit,net.minecraft.world.entity.player.Player player){
        if(!(hit instanceof net.minecraft.world.phys.EntityHitResult e)||!isProxy(e.getEntity())||player==null)return hit;
        return player.pick(player.blockInteractionRange(),1f,false);
    }
    public static String proxyId(Entity entity){return ids.get(entity.getUUID());}
    public static double[] position(UUID id){return positions.get(id);}
    public static boolean hasPositions(){return !positions.isEmpty();}
    static void seated(Entity entity){positions.remove(entity.getUUID());}
    /** combat-diag: at most 30 lines per second (fire/poison tick every 0.5 s). */
    private static boolean diag(){
        long second=System.nanoTime()/1_000_000_000L;
        if(second!=diagSecond){diagSecond=second;diagLines=0;}
        return ++diagLines<=30;
    }
    private static String type(Entity entity){
        return entity==null?"none":net.minecraft.core.registries.BuiltInRegistries.ENTITY_TYPE.getKey(entity.getType()).toString();
    }
    public static void forgetProxyIds(){knownProxyIds.clear();}
    public static void damage(LivingEntity entity,DamageSource source,float before){
        if(!tracked(entity))return;
        float after=entity.getHealth();
        // Fatal damage is deferred until the host confirms its native death/immune rules.
        float delta=before-(after<=.0011f?0:after);
        // Preview122 (user: attacking must cost stamina): a landed melee hit on a Wukong enemy adds exhaustion (vanilla 0.1 + this).
        if(!(entity instanceof ServerPlayer)&&source.getEntity() instanceof ServerPlayer attacker&&source.getDirectEntity()==attacker
            &&source.is(net.minecraft.world.damagesource.DamageTypes.PLAYER_ATTACK))attacker.causeFoodExhaustion((float)Balance.attackExhaustion());
        if(delta>0&&!(entity instanceof ServerPlayer)){
            String pid=ids.get(entity.getUUID());
            float bonus=pid==null?0:Enchants.onProxyHit(entity,pid,kinds.getOrDefault(entity.getUUID(),""),source);
            if(bonus>0){entity.setHealth(Math.max(.001f,entity.getHealth()-bonus));after=entity.getHealth();delta+=bonus;}
        }
        String owner=source.getEntity()==null?"":source.getEntity() instanceof ServerPlayer?"player":ids.getOrDefault(source.getEntity().getUUID(),"");
        String reason=source.typeHolder().unwrapKey().map(k->k.location().toString()).orElse(source.getMsgId());
        if(diag()){
            String weapon=source.getEntity() instanceof ServerPlayer p?net.minecraft.core.registries.BuiltInRegistries.ITEM.getKey(p.getMainHandItem().getItem()).toString():"-";
            Passthrough.LOG.info("DQJ combat-diag hit target={} amount={} reason={} direct={} causing={} weapon={} hpBefore={} hpAfter={}",
                entity instanceof ServerPlayer?"player":ids.getOrDefault(entity.getUUID(),"?"),delta,reason,type(source.getDirectEntity()),type(source.getEntity()),weapon,before,after);
        }
        emit(entity,delta,reason,owner,!(entity instanceof ServerPlayer)&&heavyHit(source));
    }
    /** Preview126 (user: one combo staggers and knocks an enemy back on every hit; judge poise by the attack, not damage):
     *  a heavy hit is a vanilla critical (falling, not sprinting), an axe or mace swing, a critical arrow or an explosion.
     *  The host's poise meter decides whether any hit staggers; only heavy hits may pick the knockback reaction. */
    static boolean heavyHit(DamageSource source){
        if(source.is(net.minecraft.tags.DamageTypeTags.IS_EXPLOSION))return true;
        var direct=source.getDirectEntity();
        if(direct instanceof net.minecraft.world.entity.projectile.AbstractArrow arrow)return arrow.isCritArrow();
        if(source.getEntity() instanceof ServerPlayer p&&direct==p){
            var item=p.getMainHandItem().getItem();
            if(item instanceof net.minecraft.world.item.AxeItem||item instanceof net.minecraft.world.item.MaceItem)return true;
            return p.fallDistance>0&&!p.onGround()&&!p.onClimbable()&&!p.isInWater()&&!p.isPassenger()&&!p.isSprinting();
        }
        return false;
    }
    private static void emit(LivingEntity entity,float amount,String reason){
        emit(entity,amount,reason,"");
    }
    private static void emit(LivingEntity entity,float amount,String reason,String source){emit(entity,amount,reason,source,false);}
    private static void emit(LivingEntity entity,float amount,String reason,String source,boolean heavy){
        observed.put(entity.getUUID(),entity.getHealth());
        if(!Float.isFinite(amount)||Math.abs(amount)<.0001)return;
        String id=entity instanceof ServerPlayer?"player":ids.get(entity.getUUID());
        if(id==null)return;
        long n=++sequence;pending.put(n,new Delta(id,amount));
        JsonObject out=new JsonObject();out.addProperty("t","health_delta");
        out.addProperty("session",session);out.addProperty("seq",n);out.addProperty("id",id);
        out.addProperty("amount",amount);out.addProperty("reason",reason);
        out.addProperty("source",source);
        if(heavy)out.addProperty("hk","heavy");
        Passthrough.events.accept(out.toString());
    }
    /** Server thread only: live proxies by host enemy id (ProxyFx). */
    static Map<String,Pig> proxies(){return targets;}
    public static void sync(JsonObject message){
        var s=server;if(s==null)return;s.execute(()->apply(message));
    }
    public static void hostHealth(JsonObject message){
        var s=server;if(s==null)return;s.execute(()->{
            if(!message.get("session").getAsString().equals(session)||!Passthrough.minecraftAuthority)return;
            long n=message.get("hn").getAsLong();if(n<=lastHostHealth)return;lastHostHealth=n;
            float amount=message.get("amount").getAsFloat();if(!Float.isFinite(amount)||Math.abs(amount)>8192)return;
            var source=targets.get(message.get("source").getAsString());
            net.minecraft.world.phys.Vec3 at=null;
            if(message.has("sp")){var sp=message.getAsJsonArray("sp");at=new net.minecraft.world.phys.Vec3(sp.get(0).getAsDouble(),sp.get(1).getAsDouble(),sp.get(2).getAsDouble());}
            for(var p:s.getPlayerList().getPlayers()){
                if(amount>0){
                    // C1: tracked proxy -> mob attack from it; otherwise a positioned mob
                    // attack so vanilla shield/direction rules still apply; generic only
                    // when the host reported no attacker at all.
                    net.minecraft.world.damagesource.DamageSource damage;String kind;
                    if(source!=null){damage=p.damageSources().mobAttack(source);kind="proxy";}
                    else if(at!=null&&Double.isFinite(at.x)&&Double.isFinite(at.y)&&Double.isFinite(at.z)){
                        damage=new net.minecraft.world.damagesource.DamageSource(s.registryAccess().lookupOrThrow(net.minecraft.core.registries.Registries.DAMAGE_TYPE)
                            .getOrThrow(net.minecraft.world.damagesource.DamageTypes.MOB_ATTACK),at);kind="positioned";
                    }else{damage=p.damageSources().generic();kind="generic";}
                    float before=p.getHealth();boolean blocking=p.isBlocking(),blockable=p.isDamageSourceBlocked(damage);
                    // Preview82 (user rule 2026-10-16, replaces Preview55's all-directions rule): a raised
                    // shield blocks every routed host hit from the front half (side/back are not blocked),
                    // without vanilla's 5-tick raise delay. Raised = shield in a hand and either the server
                    // is using it or the client holds the use key (Better Combat swings and native hit
                    // reactions can drop isUsingItem for a few ticks), plus a 10-tick grace after either.
                    var shieldHand=shieldHand(p);
                    boolean using=p.isUsingItem()&&p.getUseItem().getItem() instanceof net.minecraft.world.item.ShieldItem;
                    boolean held=Passthrough.useHeld&&shieldHand!=null;
                    long tick=s.overworld().getGameTime();
                    if(using||held)lastShieldTick=tick;
                    boolean raised=shieldHand!=null&&(using||held||tick-lastShieldTick<=10);
                    net.minecraft.world.phys.Vec3 from=source!=null?source.position():at;
                    double facing=front(p,from);
                    boolean front=facing>=0;
                    var shieldStack=shieldHand==null?net.minecraft.world.item.ItemStack.EMPTY:p.getItemInHand(shieldHand);
                    boolean worn=raised&&front&&shieldStack.isDamageableItem()&&shieldStack.getDamageValue()>=shieldStack.getMaxDamage()-1;
                    if(worn){
                        // Preview122: a worn-out shield blocks but cannot parry: part of the hit gets through, no durability left to lose.
                        p.causeFoodExhaustion((float)Balance.shieldExhaustion());
                        amount=(float)(amount*Balance.wornShieldDamage());
                        s.overworld().playSound(null,p.blockPosition(),net.minecraft.sounds.SoundEvents.SHIELD_BLOCK,net.minecraft.sounds.SoundSource.PLAYERS,.6f,.6f);
                        if(tick-lastWornNotice>200){lastWornNotice=tick;p.displayClientMessage(net.minecraft.network.chat.Component.literal("Shield worn out: it only blocks part of each hit. Repair it at the Shrine Trader."),true);}
                        if(diag())Passthrough.LOG.info("DQJ combat-diag host-hit worn shield blocked partly: amount now {}",amount);
                    }
                    if(raised&&front&&!worn){
                        var shield=p.getItemInHand(shieldHand);
                        p.causeFoodExhaustion((float)Balance.shieldExhaustion()); // Preview122: a parry costs 1.0 exhaustion
                        shield.hurtAndBreak(1+(int)Math.floor(amount),p,shieldHand==net.minecraft.world.InteractionHand.MAIN_HAND
                            ?net.minecraft.world.entity.EquipmentSlot.MAINHAND:net.minecraft.world.entity.EquipmentSlot.OFFHAND);
                        s.overworld().playSound(null,p.blockPosition(),net.minecraft.sounds.SoundEvents.SHIELD_BLOCK,net.minecraft.sounds.SoundSource.PLAYERS,1f,.9f+s.overworld().random.nextFloat()*.2f);
                        if(diag())Passthrough.LOG.info("DQJ combat-diag host-hit amount={} source={} kind={} shieldRaised=true blocked=true using={} held={} facing={} hp={}",amount,message.get("source").getAsString(),kind,using,held,String.format("%.2f",facing),before);
                        continue;
                    }
                    if(diag()&&shieldHand!=null)Passthrough.LOG.info("DQJ combat-diag shield-miss source={} raised={} using={} held={} sinceRaised={} facing={} (front>=0)",
                        message.get("source").getAsString(),raised,using,held,tick-lastShieldTick,String.format("%.2f",facing));
                    // Preview89: tier armor reduction (Balance) instead of vanilla's formula; the dqj:wukong_attack type
                    // bypasses vanilla armor, keeps Protection/Resistance/absorption, and armor still wears.
                    var wukong=s.registryAccess().lookupOrThrow(net.minecraft.core.registries.Registries.DAMAGE_TYPE)
                        .get(net.minecraft.resources.ResourceKey.create(net.minecraft.core.registries.Registries.DAMAGE_TYPE,net.minecraft.resources.ResourceLocation.fromNamespaceAndPath("dqj","wukong_attack")));
                    float dealt=amount;double reduction=0;
                    if(wukong.isPresent()){
                        reduction=Balance.armorReduction(p);dealt=(float)(amount*(1-reduction));
                        damage=source!=null?new net.minecraft.world.damagesource.DamageSource(wukong.get(),source):at!=null?new net.minecraft.world.damagesource.DamageSource(wukong.get(),at):new net.minecraft.world.damagesource.DamageSource(wukong.get());
                        int wear=Math.max(1,(int)(amount/4));
                        for(var slot:new net.minecraft.world.entity.EquipmentSlot[]{net.minecraft.world.entity.EquipmentSlot.HEAD,net.minecraft.world.entity.EquipmentSlot.CHEST,net.minecraft.world.entity.EquipmentSlot.LEGS,net.minecraft.world.entity.EquipmentSlot.FEET}){
                            var piece=p.getItemBySlot(slot);if(!piece.isEmpty()&&piece.isDamageableItem())piece.hurtAndBreak(wear,p,slot);
                        }
                    }
                    p.hurtServer(s.overworld(),damage,dealt);
                    if(diag())Passthrough.LOG.info("DQJ combat-diag armor raw={} reduction={} dealt={} armor={} toughness={}",amount,String.format("%.2f",reduction),dealt,p.getArmorValue(),p.getAttributeValue(net.minecraft.world.entity.ai.attributes.Attributes.ARMOR_TOUGHNESS));
                    if(diag())Passthrough.LOG.info("DQJ combat-diag host-hit amount={} source={} kind={} blocking={} blockable={} hpBefore={} hpAfter={}",
                        amount,message.get("source").getAsString(),kind,blocking,blockable,before,p.getHealth());
                }
                else if(amount<0)p.heal(-amount);
            }
        });
    }
    private static long lastShieldTick=Long.MIN_VALUE/2,lastWornNotice=Long.MIN_VALUE/2;
    private static net.minecraft.world.InteractionHand shieldHand(net.minecraft.server.level.ServerPlayer p){
        // Live114: aiming a bow/crossbow/gun with right-click is not a raised off-hand shield.
        if(p.getOffhandItem().getItem() instanceof net.minecraft.world.item.ShieldItem&&!Balance.rangedMain(p))return net.minecraft.world.InteractionHand.OFF_HAND;
        if(p.getMainHandItem().getItem() instanceof net.minecraft.world.item.ShieldItem)return net.minecraft.world.InteractionHand.MAIN_HAND;
        return null;
    }
    /** Horizontal cosine between the player's view and the direction to the attacker; >=0 = front half.
     *  Unknown attacker position counts as front (1). */
    private static double front(net.minecraft.server.level.ServerPlayer p,net.minecraft.world.phys.Vec3 from){
        if(from==null||!Double.isFinite(from.x)||!Double.isFinite(from.z))return 1;
        double dx=from.x-p.getX(),dz=from.z-p.getZ(),len=Math.sqrt(dx*dx+dz*dz);if(len<1e-3)return 1;
        var look=p.getViewVector(1f);double ll=Math.sqrt(look.x*look.x+look.z*look.z);if(ll<1e-3)return 1;
        return (dx*look.x+dz*look.z)/(len*ll);
    }
    public static void healed(LivingEntity entity,float before){
        if(tracked(entity))emit(entity,before-entity.getHealth(),"native_heal");
    }
    private static float outstanding(String id){
        String parent=MultipartTargets.parentId(id);
        float sum=0;for(var d:pending.values())if(MultipartTargets.parentId(d.id).equals(parent))sum+=d.amount;return sum;
    }
    private static void apply(JsonObject m){
        String next=m.get("session").getAsString();
        if(!next.equals(session)){clear();session=next;}
        received=System.nanoTime();ack=Math.max(ack,m.get("ack").getAsLong());
        // Live112 (user: a monster became invincible after a pearl teleport): a >3 s sync pause clears this side and restarted
        // the hit sequence at 1, while the host still held ack=27 and dropped every hit as a duplicate. Continue after the host's ack.
        if(sequence<ack)sequence=ack;
        pending.entrySet().removeIf(e->e.getKey()<=ack);
        applying=true;
        try {
            for(var p:server.getPlayerList().getPlayers()){
                if(p.isDeadOrDying())continue; // host HP must not revive a dead MC player
                float hp=m.get("hp").getAsFloat();
                p.setHealth(Math.clamp(hp-outstanding("player"),.001f,p.getMaxHealth()));
                observed.put(p.getUUID(),p.getHealth());
            }
            Set<String> keep=new HashSet<>();
            JsonArray entities=m.getAsJsonArray("entities");
            if(entities.size()>128)throw new IllegalArgumentException("Too many host targets");
            for(var entry:entities){
                var e=entry.getAsJsonObject();String id=e.get("id").getAsString();keep.add(id);
                float hp=e.get("hp").getAsFloat(),max=e.get("max").getAsFloat();
                if(!Float.isFinite(hp)||!Float.isFinite(max)||max<=0)continue;
                Pig mob=targets.get(id);
                if(mob==null||mob.isRemoved()){
                    mob=EntityType.PIG.create(server.overworld(),EntitySpawnReason.COMMAND);
                    if(mob==null)continue;
                    mob.setNoAi(true);mob.setNoGravity(true);mob.setSilent(true);mob.setInvisible(true);
                    mob.setPersistenceRequired();mob.noPhysics=true;mob.addTag("dqj.host_proxy");
                    mob.getAttribute(Attributes.ARMOR).setBaseValue(0);
                    mob.getAttribute(Attributes.KNOCKBACK_RESISTANCE).setBaseValue(1);
                    // Preview126 (user: no block placing or fire next to a Boss): LivingEntity sets blocksBuilding, so a 10 m
                    // hit-assist box refused every block inside it. Proxies are aiming aids, not bodies in the way.
                    mob.blocksBuilding=false;
                    targets.put(id,mob);ids.put(mob.getUUID(),id);knownProxyIds.add(mob.getUUID());
                    server.overworld().addFreshEntity(mob);
                }
                sizes.put(mob.getUUID(),new float[]{Math.clamp(e.get("w").getAsFloat(),.1f,16f),Math.clamp(e.get("h").getAsFloat(),.1f,32f)});
                kinds.put(mob.getUUID(),e.has("k")?e.get("k").getAsString():"");
                if(e.has("big")&&e.get("big").getAsBoolean())big.add(mob.getUUID());else big.remove(mob.getUUID());
                mob.refreshDimensions();
                var pos=e.getAsJsonArray("p");double x=pos.get(0).getAsDouble(),y=pos.get(1).getAsDouble(),z=pos.get(2).getAsDouble();
                if(!Double.isFinite(x)||!Double.isFinite(y)||!Double.isFinite(z))continue;
                ProxyFx.capture(id,mob.getDeltaMovement()); // D1: knockback is read before the per-frame reset
                // Live112 polish: a proxy seated in a boat/minecart stays in it (the host freezes the real monster).
                if(!mob.isPassenger()){mob.teleportTo(x,y,z);positions.put(mob.getUUID(),new double[]{x,y,z});}
                else seated(mob);
                mob.setDeltaMovement(0,0,0);
                mob.getAttribute(Attributes.MAX_HEALTH).setBaseValue(Math.clamp(max,1,1024));
                mob.setHealth(Math.clamp(hp-outstanding(id),.001f,mob.getMaxHealth()));
                observed.put(mob.getUUID(),mob.getHealth());
            }
            for(var iter=targets.entrySet().iterator();iter.hasNext();){var e=iter.next();if(!keep.contains(e.getKey())){
                var mob=e.getValue();ids.remove(mob.getUUID());sizes.remove(mob.getUUID());kinds.remove(mob.getUUID());big.remove(mob.getUUID());observed.remove(mob.getUUID());positions.remove(mob.getUUID());mob.discard();iter.remove();
            }}
        }finally{applying=false;}
    }
    /** Fire, cactus, sweet berry bush, cobweb, campfire (inside) and magma (step-on) on a proxy.
     *  Host terrain proxies (barriers) are skipped; lava already applies through baseTick. */
    private static void contact(net.minecraft.server.level.ServerLevel level,LivingEntity mob){
        if(mob.isRemoved()||mob.isDeadOrDying())return;
        var box=mob.getBoundingBox().deflate(1.0E-5);
        // Enlarged aiming aids are not 1000-block environmental contact volumes.
        if(MultipartTargets.isPart(mob)){
            var centre=box.getCenter();
            box=new net.minecraft.world.phys.AABB(centre.x-1,centre.y-1,centre.z-1,centre.x+1,centre.y+1,centre.z+1);
        }
        for(var pos:net.minecraft.core.BlockPos.betweenClosed(net.minecraft.util.Mth.floor(box.minX),net.minecraft.util.Mth.floor(box.minY),net.minecraft.util.Mth.floor(box.minZ),
                net.minecraft.util.Mth.floor(box.maxX),net.minecraft.util.Mth.floor(box.maxY),net.minecraft.util.Mth.floor(box.maxZ))){
            if(!level.isLoaded(pos))continue;
            var state=level.getBlockState(pos);
            if(state.isAir()||state.is(net.minecraft.world.level.block.Blocks.BARRIER)||WorldBridge.isHostProxy(pos))continue;
            try{state.entityInside(level,pos,mob);}catch(RuntimeException e){Passthrough.LOG.warn("DQJ contact {} {}",state.getBlock(),e.toString());}
            if(mob.isRemoved())return;
        }
        var on=net.minecraft.core.BlockPos.containing(mob.getX(),mob.getY()-0.2,mob.getZ());
        if(!level.isLoaded(on)||WorldBridge.isHostProxy(on))return;
        var below=level.getBlockState(on);
        if(!below.isAir()&&!below.is(net.minecraft.world.level.block.Blocks.BARRIER))
            try{below.getBlock().stepOn(level,on,below,mob);}catch(RuntimeException e){Passthrough.LOG.warn("DQJ step-on {} {}",below.getBlock(),e.toString());}
    }
    public static void tick(MinecraftServer s){
        if(!session.isEmpty() && (System.nanoTime()-received>3_000_000_000L || !Passthrough.minecraftAuthority)){clear();return;}
        // Preview71 (user item 7): contact blocks act on Wukong enemies. Proxies are noPhysics, so
        // vanilla never runs their inside-block/step-on checks; run the same block hooks here.
        if(!session.isEmpty()){
            MultipartTargets.mergeEffects();
            for(var entity:targets.values())contact(s.overworld(),entity);
        }
        // Also catch native food regeneration and mods which directly alter health.
        for(var entity:targets.values())observe(entity);
        if(!session.isEmpty())for(var p:s.getPlayerList().getPlayers()){
            // Keep real gold hearts: routed native damage already passes through MC absorption.
            GoldenApple.protectTick(p);
            observe(p);
        }
    }
    private static void observe(LivingEntity entity){
        if(!tracked(entity)||MultipartTargets.isPart(entity))return;
        Float old=observed.get(entity.getUUID());if(old!=null)emit(entity,old-entity.getHealth(),"native_health");
        observed.put(entity.getUUID(),entity.getHealth());
    }
    public static JsonObject state(){
        JsonObject out=new JsonObject();out.addProperty("session",session);out.addProperty("seq",sequence);out.addProperty("ack",ack);
        if(server!=null){
            out.addProperty("serverTime",server.overworld().getGameTime());
            JsonArray players=new JsonArray();for(var p:server.getPlayerList().getPlayers()){
                JsonObject row=new JsonObject();row.addProperty("hp",p.getHealth());
                JsonObject scores=new JsonObject();for(String key:java.util.List.of("gbg.cooldown","gbg.reloading_time","gbg.id")){
                    var objective=server.getScoreboard().getObjective(key);if(objective!=null){var score=server.getScoreboard().getPlayerScoreInfo(p,objective);if(score!=null)scores.addProperty(key,score.value());}
                }row.add("gunScores",scores);JsonArray inventory=new JsonArray();for(var item:p.getInventory().items){
                    if(item.isEmpty())continue;JsonObject v=new JsonObject();v.addProperty("name",item.getHoverName().getString());v.addProperty("count",item.getCount());inventory.add(v);
                }row.add("inventory",inventory);players.add(row);
            }out.add("players",players);
        }
        out.addProperty("pending",pending.size());JsonArray list=new JsonArray();
        for(var e:targets.entrySet()){
            JsonObject v=new JsonObject();var mob=e.getValue();v.addProperty("id",e.getKey());v.addProperty("uuid",mob.getUUID().toString());v.addProperty("hp",mob.getHealth());
            v.addProperty("width",mob.getBoundingBox().getXsize());v.addProperty("height",mob.getBoundingBox().getYsize());list.add(v);
        }
        out.add("targets",list);return out;
    }
    public static void clear(){
        ProxyFx.clear();
        for(var mob:targets.values())mob.discard();targets.clear();ids.clear();sizes.clear();kinds.clear();big.clear();observed.clear();pending.clear();positions.clear();
        session="";sequence=ack=lastHostHealth=0;
    }
    public static void report(org.java_websocket.WebSocket conn,JsonObject request){
        var s=server;if(s!=null)s.execute(()->{var out=state();out.addProperty("t","combat_state");if(request.has("req"))out.add("req",request.get("req"));conn.send(out.toString());});
    }
}
