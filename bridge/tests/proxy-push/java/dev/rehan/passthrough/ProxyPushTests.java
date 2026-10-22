package dev.rehan.passthrough;

import com.google.gson.*;
import net.fabricmc.fabric.api.gametest.v1.FabricGameTest;
import net.minecraft.gametest.framework.*;
import net.minecraft.world.entity.*;
import net.minecraft.world.phys.*;

/** Entity.noPhysics is not network-synced. Reproduce the client flags, using the real common push method. */
public final class ProxyPushTests implements FabricGameTest {
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=80)
    public void giantPartsCannotMoveAnIdlePlayer(GameTestHelper h)throws Exception{
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();
        p.setGameMode(net.minecraft.world.level.GameType.SURVIVAL);p.setClientLoaded(true);p.getAbilities().invulnerable=false;
        Passthrough.active=Passthrough.minecraftAuthority=true;CombatBridge.attach(level.getServer());
        try{
            var at=h.absolutePos(new net.minecraft.core.BlockPos(4,16,4)).getCenter();
            var m=new JsonObject();m.addProperty("session","push-test");m.addProperty("hp",20);m.addProperty("ack",0);
            var rows=new JsonArray();
            for(int i=-1;i<24;i++){
                var row=new JsonObject();row.addProperty("id",i<0?"dragon":"dragon#"+i);
                row.addProperty("hp",1000);row.addProperty("max",1000);row.addProperty("w",10);row.addProperty("h",10);row.addProperty("big",true);
                var pos=new JsonArray();pos.add(at.x+.2);pos.add(at.y-2);pos.add(at.z+.5+(i+1)*.1);row.add("p",pos);rows.add(row);
            }
            m.add("entities",rows);var apply=CombatBridge.class.getDeclaredMethod("apply",JsonObject.class);apply.setAccessible(true);apply.invoke(null,m);
            p.setPos(at);p.setDeltaMovement(Vec3.ZERO);
            // Server sets noPhysics=true; a newly spawned client copy has noPhysics=false.
            // Exercise both receiver/caller directions so only fixing the invisible mob's own push is insufficient.
            for(var part:CombatBridge.proxies().values())part.noPhysics=false;
            for(int tick=0;tick<120;tick++){
                for(var part:CombatBridge.proxies().values())if(part.getBoundingBox().intersects(p.getBoundingBox())){
                    part.push(p);p.push(part);
                }
                var v=p.getDeltaMovement();p.setPos(p.position().add(v.x,0,v.z));p.setDeltaMovement(v.scale(.6));
            }
            double drift=p.position().subtract(at).horizontalDistance();
            h.assertTrue(drift<.00001,"idle player must not be pushed backwards by enlarged proxy hitboxes; drift="+drift);
            for(var part:CombatBridge.proxies().values())h.assertFalse(part.isPushable(),"proxy excluded from vanilla body-push candidate list");
            // Body pushing is separate from attack knockback. Do not clear player velocity every tick.
            p.setDeltaMovement(Vec3.ZERO);p.knockback(.4,0,1);
            h.assertTrue(p.getDeltaMovement().horizontalDistanceSqr()>.01,"real attack knockback remains enabled");
            var normal=EntityType.PIG.create(level,EntitySpawnReason.COMMAND);normal.setPos(at.x+.2,at.y,at.z+.5);
            p.setPos(at);p.setDeltaMovement(Vec3.ZERO);normal.push(p);
            h.assertTrue(normal.isPushable()&&p.getDeltaMovement().horizontalDistanceSqr()>0,"ordinary mobs still push normally");
            var part=CombatBridge.proxies().get("dragon#23");var root=CombatBridge.proxies().get("dragon");
            root.invulnerableTime=0;float before=root.getHealth();part.hurtServer(level,level.damageSources().generic(),3);
            h.assertTrue(root.getHealth()<before,"enlarged part remains damageable and routes to parent");
            var start=at.add(0,1,-6);var end=start.add(0,0,4);p.setPos(start);
            var hit=net.minecraft.world.entity.projectile.ProjectileUtil.getEntityHitResult(p,start,end,new AABB(start,end).inflate(1),
                e->CombatBridge.isProxy(e)&&e.isPickable(),16);
            h.assertTrue(hit!=null,"non-pushable proxy still participates in actual MC targeting");
            Passthrough.LOG.info("Preview123 proxy push PASS: 120 idle ticks against 25 enlarged client-flag proxies, no drift; ordinary collisions and attack knockback preserved; ray targeting and parent damage preserved");
            h.succeed();
        }finally{CombatBridge.clear();Passthrough.active=Passthrough.minecraftAuthority=false;}
    }

    /** Preview123: F8 native control freezes the idle MC avatar (position, gravity, food), blocks MC damage, and the hold
     *  ends on release or 3 s after the last host heartbeat. */
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=100)
    public void nativeControlFreezesSteve(GameTestHelper h)throws Exception{
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();p.setGameMode(net.minecraft.world.level.GameType.SURVIVAL);
        p.getAbilities().invulnerable=false;p.setHealth(20);p.getFoodData().setFoodLevel(15);
        p.setClientLoaded(true); // a mock player is damage-immune until its client counts as loaded
        var start=p.position();
        var expires=NativePause.class.getDeclaredField("expires");expires.setAccessible(true);
        var tick=NativePause.class.getDeclaredMethod("tick",net.minecraft.server.MinecraftServer.class);tick.setAccessible(true);
        expires.setLong(null,System.nanoTime()+3_000_000_000L);tick.invoke(null,level.getServer());
        h.assertTrue(NativePause.held(p)&&p.isNoGravity(),"F8 hold freezes Steve (no gravity)");
        p.teleportTo(start.x+5,start.y+3,start.z);p.getFoodData().setFoodLevel(3);tick.invoke(null,level.getServer());
        h.assertTrue(p.position().distanceTo(start)<1e-3,"held position restored");
        h.assertTrue(p.getFoodData().getFoodLevel()==15,"food kept while Wukong plays");
        var src=p.damageSources().generic();p.invulnerableTime=0;p.hurtServer(level,src,6);
        h.assertTrue(p.getHealth()==20,"no MC damage while held");
        NativePause.clear(level.getServer());
        h.assertFalse(NativePause.held(p),"release ends the hold");h.assertFalse(p.isNoGravity(),"gravity restored on release");
        expires.setLong(null,System.nanoTime()+3_000_000_000L);tick.invoke(null,level.getServer());h.assertTrue(NativePause.held(p),"held again");
        expires.setLong(null,System.nanoTime()-1);tick.invoke(null,level.getServer());
        h.assertFalse(NativePause.held(p)||p.isNoGravity(),"hold expires without host heartbeat (crash/disconnect safety)");
        p.invulnerableTime=0;p.hurtServer(level,src,6);h.assertTrue(p.getHealth()<20,"MC damage works again after the hold");
        boolean wasActive=Passthrough.active;Passthrough.active=true;float hp=p.getHealth();p.invulnerableTime=0;
        p.hurtServer(level,p.damageSources().enderPearl(),5);h.assertTrue(p.getHealth()==hp,"Preview125: ender pearl self-damage dropped while bridged");
        Passthrough.active=false;p.invulnerableTime=0;p.hurtServer(level,p.damageSources().enderPearl(),5);h.assertTrue(p.getHealth()<hp,"vanilla pearl damage when not bridged");Passthrough.active=wasActive;
        Passthrough.LOG.info("Preview123 native control PASS: freeze, food kept, damage blocked, release and heartbeat expiry");
        h.succeed();
    }
}
