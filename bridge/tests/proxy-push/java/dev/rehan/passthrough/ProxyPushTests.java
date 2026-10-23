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
    /** Preview126 (user: next to a Boss no block placing and no fire; Live125 21:34 40 of 59 use presses hit a proxy):
     *  enlarged hitboxes never block building, and a use press aimed at one goes to the block behind it. */
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=60)
    public void bossHitboxesDoNotBlockBuildingOrUse(GameTestHelper h)throws Exception{
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();p.setGameMode(net.minecraft.world.level.GameType.SURVIVAL);
        Passthrough.active=Passthrough.minecraftAuthority=true;CombatBridge.attach(level.getServer());
        try{
            var at=h.absolutePos(new net.minecraft.core.BlockPos(4,16,8)).getCenter();
            var m=new JsonObject();m.addProperty("session","use-test");m.addProperty("hp",20);m.addProperty("ack",0);
            var rows=new JsonArray();
            for(int i=-1;i<24;i++){
                var row=new JsonObject();row.addProperty("id",i<0?"boss":"boss#"+i);
                row.addProperty("hp",1000);row.addProperty("max",1000);row.addProperty("w",10);row.addProperty("h",10);row.addProperty("big",true);
                var pos=new JsonArray();pos.add(at.x);pos.add(at.y-5);pos.add(at.z);row.add("p",pos);rows.add(row);
            }
            m.add("entities",rows);var apply=CombatBridge.class.getDeclaredMethod("apply",JsonObject.class);apply.setAccessible(true);apply.invoke(null,m);
            var stone=net.minecraft.world.level.block.Blocks.STONE.defaultBlockState();
            var inside=net.minecraft.core.BlockPos.containing(at.x+2,at.y,at.z+1);
            for(var part:CombatBridge.proxies().values())h.assertTrue(part.getBoundingBox().intersects(new AABB(inside)),"test cell lies inside every enlarged box");
            h.assertTrue(level.isUnobstructed(stone,inside,net.minecraft.world.phys.shapes.CollisionContext.empty()),"a block can be placed inside an enlarged Boss hitbox");
            var pig=EntityType.PIG.create(level,EntitySpawnReason.COMMAND);var pigCell=h.absolutePos(new net.minecraft.core.BlockPos(14,2,2));
            pig.setPos(pigCell.getCenter().x,pigCell.getY(),pigCell.getCenter().z);level.addFreshEntity(pig);
            h.assertFalse(level.isUnobstructed(stone,pigCell,net.minecraft.world.phys.shapes.CollisionContext.empty()),"an ordinary mob still blocks building");
            // Use press: player outside the boxes looking at a stone block behind them.
            var target=net.minecraft.core.BlockPos.containing(at.x,at.y,at.z+3);level.setBlockAndUpdate(target,stone);
            var eye=new Vec3(at.x+.5-.5,at.y-p.getEyeHeight(),at.z-1.2);p.teleportTo(eye.x,eye.y,eye.z);p.setYRot(0);p.setXRot(0);p.setOldPosAndRot();
            var start=p.getEyePosition();var end=start.add(p.getViewVector(1f).scale(p.blockInteractionRange()));
            var hit=net.minecraft.world.entity.projectile.ProjectileUtil.getEntityHitResult(p,start,end,new AABB(start,end).inflate(1),e->CombatBridge.isProxy(e)&&e.isPickable(),p.blockInteractionRange()*p.blockInteractionRange());
            h.assertTrue(hit!=null,"crosshair ray hits an enlarged proxy first (the live failure)");
            var use=CombatBridge.useTarget(hit,p);
            h.assertTrue(use instanceof net.minecraft.world.phys.BlockHitResult b&&use.getType()==HitResult.Type.BLOCK&&b.getBlockPos().equals(target),"use press goes through the proxy to the stone block; got "+use);
            var pigHit=new EntityHitResult(pig);
            h.assertTrue(CombatBridge.useTarget(pigHit,p)==pigHit,"right-click on an ordinary mob is unchanged");
            pig.discard();
            Passthrough.LOG.info("Preview126 proxy use PASS: building inside 25 enlarged boxes allowed, ordinary mob still blocks, use press reaches the block behind the Boss");
            h.succeed();
        }finally{CombatBridge.clear();Passthrough.active=Passthrough.minecraftAuthority=false;}
    }
    /** Preview126 (user: blocks too hard to break; planks/dirt one hit, obsidian-like 2-3 hits, a Boss breaks them at once). */
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=40)
    public void enemyHitsBreakBlocksByTier(GameTestHelper h){
        var level=h.getLevel();
        var dirt=net.minecraft.world.level.block.Blocks.DIRT.defaultBlockState();var planks=net.minecraft.world.level.block.Blocks.OAK_PLANKS.defaultBlockState();
        var cobble=net.minecraft.world.level.block.Blocks.COBBLESTONE.defaultBlockState();var bricks=net.minecraft.world.level.block.Blocks.STONE_BRICKS.defaultBlockState();
        var obsidian=net.minecraft.world.level.block.Blocks.OBSIDIAN.defaultBlockState();
        h.assertTrue(BlockToughness.tier(dirt)==1&&BlockToughness.tier(planks)==1&&BlockToughness.tier(net.minecraft.world.level.block.Blocks.GLASS.defaultBlockState())==1,"dirt, planks, glass: one hit");
        h.assertTrue(BlockToughness.tier(cobble)==2&&BlockToughness.tier(bricks)==2,"cobblestone, stone bricks: two hits");
        h.assertTrue(BlockToughness.tier(obsidian)==3,"obsidian: three hits");
        h.assertTrue(BlockToughness.hitsToBreak(cobble,true)==1&&BlockToughness.hitsToBreak(dirt,true)==1&&BlockToughness.hitsToBreak(obsidian,true)==2,"Boss: one hit for soft/stone, two for obsidian");
        BlockToughness.clear();
        var a=h.absolutePos(new net.minecraft.core.BlockPos(1,2,1));var b=h.absolutePos(new net.minecraft.core.BlockPos(2,2,1));
        var c=h.absolutePos(new net.minecraft.core.BlockPos(3,2,1));var d=h.absolutePos(new net.minecraft.core.BlockPos(4,2,1));var e=h.absolutePos(new net.minecraft.core.BlockPos(5,2,1));
        level.setBlockAndUpdate(a,planks);level.setBlockAndUpdate(b,cobble);level.setBlockAndUpdate(c,obsidian);level.setBlockAndUpdate(d,obsidian);
        level.setBlockAndUpdate(e,net.minecraft.world.level.block.Blocks.BARRIER.defaultBlockState());
        h.assertTrue(BlockToughness.hit(level,a,false)&&level.getBlockState(a).isAir(),"planks break on the first enemy hit");
        h.assertFalse(BlockToughness.hit(level,b,false),"cobblestone holds the first hit");h.assertTrue(BlockToughness.hit(level,b,false)&&level.getBlockState(b).isAir(),"cobblestone breaks on the second");
        h.assertFalse(BlockToughness.hit(level,c,false)||BlockToughness.hit(level,c,false),"obsidian holds two hits");h.assertTrue(BlockToughness.hit(level,c,false),"obsidian breaks on the third");
        h.assertFalse(BlockToughness.hit(level,d,true),"obsidian holds one Boss hit");h.assertTrue(BlockToughness.hit(level,d,true),"and breaks on the second Boss hit");
        level.setBlockAndUpdate(b,cobble);h.assertTrue(BlockToughness.hit(level,b,true),"cobblestone falls to one Boss hit");
        h.assertFalse(BlockToughness.hit(level,e,true)||BlockToughness.hit(level,e,true),"barriers never break");
        var lore=ShrineShop.wall(new net.minecraft.world.item.ItemStack(net.minecraft.world.item.Items.OBSIDIAN,16)).get(net.minecraft.core.component.DataComponents.LORE);
        h.assertTrue(lore!=null&&lore.lines().get(0).getString().equals("Holds 3 enemy hits, 2 Boss hits"),"shop lore states the hits; got "+lore);
        Passthrough.LOG.info("Preview126 block toughness PASS: planks/dirt 1 hit, cobblestone 2, obsidian 3; Boss 1/1/2; barriers never; shop lore");
        h.succeed();
    }
    /** Preview126: heavy hits (may pick the knockback reaction) are decided by the attack, never by damage. */
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=20)
    public void heavyHitsAreAttackKindsNotDamage(GameTestHelper h){
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();p.setGameMode(net.minecraft.world.level.GameType.SURVIVAL);
        p.setItemInHand(net.minecraft.world.InteractionHand.MAIN_HAND,new net.minecraft.world.item.ItemStack(net.minecraft.world.item.Items.NETHERITE_SWORD));
        p.setOnGround(true);p.fallDistance=0;
        h.assertFalse(CombatBridge.heavyHit(p.damageSources().playerAttack(p)),"a grounded netherite sword hit is not heavy, however much damage it does");
        p.setOnGround(false);p.fallDistance=1.2f;p.setSprinting(false);
        h.assertTrue(CombatBridge.heavyHit(p.damageSources().playerAttack(p)),"a falling (critical) hit is heavy");
        p.setOnGround(true);p.fallDistance=0;
        p.setItemInHand(net.minecraft.world.InteractionHand.MAIN_HAND,new net.minecraft.world.item.ItemStack(net.minecraft.world.item.Items.IRON_AXE));
        h.assertTrue(CombatBridge.heavyHit(p.damageSources().playerAttack(p)),"an axe swing is heavy");
        h.assertTrue(CombatBridge.heavyHit(p.damageSources().explosion(null,null)),"an explosion is heavy");
        h.assertFalse(CombatBridge.heavyHit(p.damageSources().generic())||CombatBridge.heavyHit(p.damageSources().onFire()),"generic and fire damage are not heavy");
        var arrow=new net.minecraft.world.entity.projectile.Arrow(level,p,new net.minecraft.world.item.ItemStack(net.minecraft.world.item.Items.ARROW),null);
        h.assertFalse(CombatBridge.heavyHit(p.damageSources().arrow(arrow,p)),"a half-drawn arrow is not heavy");
        arrow.setCritArrow(true);h.assertTrue(CombatBridge.heavyHit(p.damageSources().arrow(arrow,p)),"a fully drawn (critical) arrow is heavy");
        Passthrough.LOG.info("Preview126 heavy hit PASS: sword not heavy, crit/axe/explosion/crit arrow heavy, fire/generic not");
        h.succeed();
    }
    /** Preview127 (Live126: one sword swing touched 7 Erlang Shen parts and landed twice): one attack lands once per enemy. */
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=60)
    public void oneSwingHitsAMultipartBossOnce(GameTestHelper h)throws Exception{
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();p.setGameMode(net.minecraft.world.level.GameType.SURVIVAL);
        Passthrough.active=Passthrough.minecraftAuthority=true;CombatBridge.attach(level.getServer());
        var at=h.absolutePos(new net.minecraft.core.BlockPos(4,8,4)).getCenter();
        var m=new JsonObject();m.addProperty("session","ledger-test");m.addProperty("hp",20);m.addProperty("ack",0);
        var rows=new JsonArray();
        for(int i=-1;i<9;i++){
            var row=new JsonObject();row.addProperty("id",i<0?"erlang":"erlang#"+i);
            row.addProperty("hp",1000);row.addProperty("max",1000);row.addProperty("w",10);row.addProperty("h",10);row.addProperty("big",true);
            var pos=new JsonArray();pos.add(at.x);pos.add(at.y);pos.add(at.z);row.add("p",pos);rows.add(row);
        }
        m.add("entities",rows);var apply=CombatBridge.class.getDeclaredMethod("apply",JsonObject.class);apply.setAccessible(true);apply.invoke(null,m);
        var root=CombatBridge.proxies().get("erlang");
        // Live126 pattern: the swing reaches the root and 7 parts; cooldown is reset between calls to model the observed bypass.
        float start=root.getHealth();var swing=p.damageSources().playerAttack(p);
        root.invulnerableTime=0;root.hurtServer(level,swing,17);
        for(int i=0;i<7;i++){root.invulnerableTime=0;CombatBridge.proxies().get("erlang#"+i).hurtServer(level,swing,17);}
        float afterSwing=root.getHealth();
        h.assertTrue(Math.abs(start-afterSwing-17)<.01,"one swing over 8 boxes lands exactly once; lost "+(start-afterSwing));
        var other=EntityType.ZOMBIE.create(level,EntitySpawnReason.COMMAND);other.setPos(at.x+20,at.y,at.z);level.addFreshEntity(other);
        float zombie=other.getHealth();other.hurtServer(level,swing,3);
        h.assertTrue(other.getHealth()<zombie,"an ordinary mob in the same swing still takes its hit");
        var a1=new net.minecraft.world.entity.projectile.Arrow(level,p,new net.minecraft.world.item.ItemStack(net.minecraft.world.item.Items.ARROW),null);
        var a2=new net.minecraft.world.entity.projectile.Arrow(level,p,new net.minecraft.world.item.ItemStack(net.minecraft.world.item.Items.ARROW),null);
        root.invulnerableTime=0;float b=root.getHealth();CombatBridge.proxies().get("erlang#1").hurtServer(level,p.damageSources().arrow(a1,p),5);
        root.invulnerableTime=0;CombatBridge.proxies().get("erlang#2").hurtServer(level,p.damageSources().arrow(a2,p),5);
        h.assertTrue(Math.abs(b-root.getHealth()-10)<.01,"two different arrows in one tick both count");
        h.runAfterDelay(5,()->{
            try{
                float before=root.getHealth();root.invulnerableTime=0;CombatBridge.proxies().get("erlang#3").hurtServer(level,swing,17);
                h.assertTrue(Math.abs(before-root.getHealth()-17)<.01,"the next swing (5 ticks later) lands again");
                Passthrough.LOG.info("Preview127 hit ledger PASS: one swing over 8 boxes = 1 hit, other mob hit, 2 arrows = 2 hits, next swing lands");
                other.discard();h.succeed();
            }finally{CombatBridge.clear();Passthrough.active=Passthrough.minecraftAuthority=false;}
        });
    }

    /** Preview127 (user: stuck after respawning in the Erlang Shen arena): the watchdog clears a stale hold and lifts Steve out of a block. */
    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=40)
    public void stuckRecoveryClearsKnownTraps(GameTestHelper h)throws Exception{
        var level=h.getLevel();var p=h.makeMockServerPlayerInLevel();p.setGameMode(net.minecraft.world.level.GameType.SURVIVAL);
        var cell=h.absolutePos(new net.minecraft.core.BlockPos(2,2,2));
        level.setBlockAndUpdate(cell,net.minecraft.world.level.block.Blocks.STONE.defaultBlockState());
        p.teleportTo(cell.getX()+.5,cell.getY()+.3,cell.getZ()+.5);
        var expires=NativePause.class.getDeclaredField("expires");expires.setAccessible(true);
        var tick=NativePause.class.getDeclaredMethod("tick",net.minecraft.server.MinecraftServer.class);tick.setAccessible(true);
        expires.setLong(null,System.nanoTime()+3_000_000_000L);tick.invoke(null,level.getServer());
        h.assertTrue(NativePause.held(p)&&p.isNoGravity(),"setup: Steve held and inside stone");
        String result=StuckRecovery.check(level.getServer(),p.getUUID());
        h.assertFalse(NativePause.held(p)||p.isNoGravity(),"hold and no-gravity cleared; "+result);
        h.assertTrue(level.noCollision(p,p.getBoundingBox().deflate(1e-3))&&p.getY()>=cell.getY()+1-1e-6,"lifted out of the block onto it; y="+p.getY());
        h.assertTrue(result.contains("inside a block")&&result.contains("F8/idle hold"),"reasons are named: "+result);
        String clean=StuckRecovery.check(level.getServer(),p.getUUID());
        h.assertTrue(!clean.contains("inside a block")&&!clean.contains("hold")&&clean.contains("fixes=none"),"a free Steve needs no fix: "+clean);
        Passthrough.LOG.info("Preview127 stuck recovery PASS: "+result);
        h.succeed();
    }
}
