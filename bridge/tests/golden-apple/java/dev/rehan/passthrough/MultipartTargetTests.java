package dev.rehan.passthrough;

import com.google.gson.*;
import java.util.ArrayList;
import net.fabricmc.fabric.api.gametest.v1.FabricGameTest;
import net.minecraft.gametest.framework.*;
import net.minecraft.world.entity.*;
import net.minecraft.world.item.*;
import net.minecraft.world.effect.*;
import net.minecraft.world.phys.*;

/** Real MC proxy geometry, hurt mixins, acknowledgement and shared effects, with installed mods loaded. */
public final class MultipartTargetTests implements FabricGameTest {
    private static void close(GameTestHelper h,double a,double b,String why){h.assertTrue(Math.abs(a-b)<.003,why+" actual="+a+" expected="+b);}
    private static JsonObject snapshot(Vec3 at,float hp,long ack){
        var m=new JsonObject();m.addProperty("session","dragon-test");m.addProperty("hp",20);m.addProperty("ack",ack);
        var list=new JsonArray();
        for(int i=-1;i<24;i++){
            var e=new JsonObject();e.addProperty("id",i<0?"139639":"139639#"+i);
            e.addProperty("hp",hp);e.addProperty("max",1000);e.addProperty("w",i<0?2:10);e.addProperty("h",i<0?6:10);e.addProperty("big",true);
            var p=new JsonArray();p.add(at.x+(i<0?0:45.0*i/23));p.add(at.y-5);p.add(at.z);e.add("p",p);list.add(e);
        }
        m.add("entities",list);return m;
    }
    private static void apply(JsonObject m)throws Exception{
        var method=CombatBridge.class.getDeclaredMethod("apply",JsonObject.class);method.setAccessible(true);method.invoke(null,m);
    }
    private static double total(ArrayList<JsonObject> messages){
        double sum=0;for(var m:messages)if(m.get("t").getAsString().equals("health_delta"))sum+=m.get("amount").getAsDouble();return sum;
    }
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=80)
    public void dragonGeometryAndSharedDamage(GameTestHelper h)throws Exception{
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();p.setGameMode(net.minecraft.world.level.GameType.SURVIVAL);p.setClientLoaded(true);p.getAbilities().invulnerable=false;
        var previous=Passthrough.events;var messages=new ArrayList<JsonObject>();Passthrough.events=s->messages.add(JsonParser.parseString(s).getAsJsonObject());
        Passthrough.active=Passthrough.minecraftAuthority=true;CombatBridge.attach(level.getServer());
        try{
            var origin=h.absolutePos(new net.minecraft.core.BlockPos(0,24,0)).getCenter();
            for(int x=0;x<64;x+=8)level.getChunkAt(net.minecraft.core.BlockPos.containing(origin.add(x,0,0)));
            apply(snapshot(origin,1000,0));var root=CombatBridge.proxies().get("139639");
            h.assertTrue(CombatBridge.proxies().size()==25,"one root and 24 extra parts");
            // A 3 m melee reach can hit the edge 5 m away from the visible centre; no root-only ray can reach the tail.
            for(double x=0;x<=45;x+=1.5){
                var start=origin.add(x,0,-6);var end=start.add(0,0,3);
                p.setPos(start);var ray=net.minecraft.world.entity.projectile.ProjectileUtil.getEntityHitResult(p,start,end,new AABB(start,end).inflate(1),
                    e->CombatBridge.isProxy(e)&&e.isPickable(),9);
                h.assertTrue(ray!=null,"real MC ray finds hit-assist body at "+x+" m");
                h.assertTrue(MultipartTargets.root((LivingEntity)ray.getEntity())==root,"head/body/tail ray maps to parent");
            }
            var head=CombatBridge.proxies().get("139639#23");var tail=CombatBridge.proxies().get("139639#0");
            close(h,head.getBoundingBox().getXsize(),10,"actual part width");close(h,head.getBoundingBox().getYsize(),10,"actual part height");
            p.setItemSlot(EquipmentSlot.MAINHAND,new ItemStack(Items.DIAMOND_SWORD));var melee=level.damageSources().playerAttack(p);
            float expected=Balance.outgoing(root,melee,5);root.invulnerableTime=0;messages.clear();
            head.hurtServer(level,melee,5);close(h,root.getHealth(),1000-expected,"part melee reaches root, scaling exactly once");
            tail.hurtServer(level,melee,5);close(h,total(messages),expected,"overlapping parts share hurt cooldown");
            for(var m:messages)if(m.get("t").getAsString().equals("health_delta"))h.assertTrue(m.get("id").getAsString().equals("139639"),"only parent health messages");
            apply(snapshot(origin,1000,0));
            for(var part:CombatBridge.proxies().values())close(h,part.getHealth(),1000-expected,"stale host HP cannot resurrect individual parts");
            long ack=CombatBridge.state().get("seq").getAsLong();apply(snapshot(origin,1000-expected,ack));
            close(h,root.getHealth(),1000-expected,"ACK settles damage once");
            // One explosion touches every part. Its damage is that of one root hit, not 25 root hits.
            var tnt=EntityType.TNT.create(level,EntitySpawnReason.COMMAND);var blast=level.damageSources().explosion(tnt,p);
            float blastExpected=Balance.outgoing(root,blast,6);float before=root.getHealth();root.invulnerableTime=0;messages.clear();
            for(var part:CombatBridge.proxies().values())part.hurtServer(level,blast,6);
            close(h,before-root.getHealth(),blastExpected,"TNT touching all 25 proxies damages once");close(h,total(messages),blastExpected,"one blast health delta total");
            // Actual arrow damage source and collision routing; no duplicate balance multiplication.
            var arrow=EntityType.ARROW.create(level,EntitySpawnReason.COMMAND);arrow.setOwner(p);var arrowSource=level.damageSources().arrow(arrow,p);
            root.invulnerableTime=0;before=root.getHealth();float arrowExpected=Balance.outgoing(root,arrowSource,4);
            head.hurtServer(level,arrowSource,4);close(h,before-root.getHealth(),arrowExpected,"arrow on head damages shared root");
            head.addEffect(new MobEffectInstance(MobEffects.MOVEMENT_SLOWDOWN,200,1));tail.addEffect(new MobEffectInstance(MobEffects.WEAKNESS,100,0));
            messages.clear();for(int i=0;i<5;i++)ProxyFx.tick(level.getServer());
            h.assertTrue(root.hasEffect(MobEffects.MOVEMENT_SLOWDOWN)&&root.hasEffect(MobEffects.WEAKNESS),"effects from different parts merge on root");
            int fxRows=0;
            for(var m:messages)if(m.get("t").getAsString().equals("proxy_fx"))for(var value:m.getAsJsonArray("e")){
                var row=value.getAsJsonObject();h.assertTrue(row.get("id").getAsString().equals("139639"),"effect row uses parent");
                h.assertTrue(row.get("slow").getAsInt()==1&&row.get("weak").getAsInt()==0,"neutral parts do not cancel active effects");fxRows++;
            }
            h.assertTrue(fxRows==1,"one aggregate effect report");
            var normal=EntityType.PIG.create(level,EntitySpawnReason.COMMAND);float normalBefore=normal.getHealth();normal.hurtServer(level,level.damageSources().generic(),3);
            close(h,normal.getHealth(),normalBefore-3,"ordinary MC mob retains vanilla hurt path");
            var removed=new ArrayList<>(CombatBridge.proxies().values());var empty=snapshot(origin,1000,CombatBridge.state().get("seq").getAsLong());empty.add("entities",new JsonArray());apply(empty);
            h.assertTrue(CombatBridge.proxies().isEmpty()&&removed.stream().allMatch(Entity::isRemoved),"removed dragon leaves no invisible hitboxes");
            Passthrough.LOG.info("Preview120 multipart PASS: 45m head/body/tail actual MC rays, melee, arrow, shared health ACK, TNT without multi-hit inflation, effect merge, cleanup, vanilla control");
            h.succeed();
        }finally{CombatBridge.clear();Passthrough.events=previous;Passthrough.active=Passthrough.minecraftAuthority=false;}
    }
}
