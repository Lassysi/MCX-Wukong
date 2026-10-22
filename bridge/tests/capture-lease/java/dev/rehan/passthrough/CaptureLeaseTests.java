package dev.rehan.passthrough;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import net.fabricmc.fabric.api.gametest.v1.FabricGameTest;
import net.minecraft.core.BlockPos;
import net.minecraft.gametest.framework.GameTest;
import net.minecraft.gametest.framework.GameTestHelper;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.animal.Pig;
import net.minecraft.world.phys.Vec3;

/** Real server entities in an isolated GameTest world; no player inventory or live game connection. */
public final class CaptureLeaseTests implements FabricGameTest {
    private static void sync(Vec3 pos) {
        sync(pos,false);
    }
    private static void sync(Vec3 pos,boolean big) {
        JsonObject state=new JsonObject();state.addProperty("session","capture-lease-test");
        state.addProperty("ack",0);state.addProperty("hp",20);
        JsonObject enemy=new JsonObject();enemy.addProperty("id","fixture");
        enemy.addProperty("hp",20);enemy.addProperty("max",20);
        enemy.addProperty("big",big);
        enemy.addProperty("w",2.2);enemy.addProperty("h",3.0);
        JsonArray at=new JsonArray();at.add(pos.x);at.add(pos.y);at.add(pos.z);enemy.add("p",at);
        JsonArray list=new JsonArray();list.add(enemy);state.add("entities",list);
        CombatBridge.sync(state);
    }

    @GameTest(template=EMPTY_STRUCTURE,timeoutTicks=530)
    public void boatAndCartLease(GameTestHelper h) {
        var level=h.getLevel();var origin=h.absoluteVec(new Vec3(2,3,2));
        Passthrough.active=Passthrough.minecraftAuthority=true;
        Entity boat=h.spawn(EntityType.OAK_BOAT,new Vec3(2,3,2));boat.setNoGravity(true);
        sync(origin);
        // Keep the host's old location authoritative for ordinary movement, as a real host does.
        for(int t=1;t<=230;t++){
            final int n=t;
            h.runAtTickTime(t,()->{
                Passthrough.active=Passthrough.minecraftAuthority=true;
                boat.setDeltaMovement(Vec3.ZERO);sync(origin);
                Pig pig=CombatBridge.proxies().get("fixture");
                if(pig==null)return;
                if(n==15){
                    h.assertTrue(pig.getVehicle()==boat,"ordinary proxy must board a nearby boat");
                    // Vanilla can eject a passenger; a live capture must recover.
                    pig.stopRiding();
                }
                if(n==25)h.assertTrue(pig.getVehicle()==boat,"premature dismount must not cancel the capture");
                if(n==35){
                    boat.setPos(origin.add(1,0,0));boat.positionRider(pig);
                }
                if(n==40){
                    double[] p=CombatBridge.position(pig.getUUID());
                    h.assertTrue(p==null||Math.abs(p[0]-pig.getX())<.01,"client must not snap a passenger to its stale host position");
                }
                if(n==180)h.assertTrue(pig.getVehicle()==boat,"Preview124: capture holds past 8 s until the player breaks the vehicle");
                if(n==185){
                    boat.hurtServer(level,level.damageSources().generic(),100);boat.hurtServer(level,level.damageSources().explosion(null,null),100);
                    h.assertTrue(boat.isAlive()&&pig.getVehicle()==boat,"monster blasts and other damage cannot break the vehicle");
                }
                if(n==190){var player=h.makeMockServerPlayerInLevel();boat.hurtServer(level,level.damageSources().playerAttack(player),100);h.assertFalse(boat.isAlive(),"the player can break the vehicle");}
                if(n==195)h.assertFalse(pig.isPassenger(),"breaking the vehicle releases the monster");
                if(n==230){
                    boat.discard();CombatBridge.clear();
                }
            });
        }
        Entity[] second=new Entity[1];
        h.runAtTickTime(235,()->{
            second[0]=h.spawn(EntityType.OAK_BOAT,new Vec3(2,3,2));second[0].setNoGravity(true);
            sync(origin,true);
        });
        h.runAtTickTime(240,()->{
            Pig pig=CombatBridge.proxies().get("fixture");
            h.assertTrue(pig!=null&&!pig.isPassenger(),"protected enemy cannot be captured automatically");
            h.assertFalse(pig.startRiding(second[0],true),"protected enemy rejects forced boarding too");
            second[0].discard();CombatBridge.clear();
        });
        h.runAtTickTime(250,()->{
            // Preview124: the minecart stands 4.5 blocks from the proxy centre (3.4 from the 2.2-wide hitbox): still captured.
            second[0]=h.spawn(EntityType.MINECART,new Vec3(6.5,3,2));second[0].setNoGravity(true);sync(origin);
        });
        for(int t=251;t<=480;t++){
            final int n=t;
            h.runAtTickTime(t,()->{
                Passthrough.active=Passthrough.minecraftAuthority=true;
                second[0].setDeltaMovement(Vec3.ZERO);sync(origin);
                Pig pig=CombatBridge.proxies().get("fixture");if(pig==null)return;
                if(n==265){h.assertTrue(pig.getVehicle()==second[0],"ordinary proxy is pulled into a minecart 3.4 blocks from its hitbox");pig.stopRiding();}
                if(n==275)h.assertTrue(pig.getVehicle()==second[0],"minecart capture survives an unexpected dismount");
                if(n==430)h.assertTrue(pig.getVehicle()==second[0],"minecart capture also holds past 8 s");
                if(n==435){var player=h.makeMockServerPlayerInLevel();second[0].hurtServer(level,level.damageSources().playerAttack(player),100);}
                if(n==440)h.assertFalse(pig.isPassenger(),"breaking the minecart releases the monster");
                if(n==445){var far=h.spawn(EntityType.OAK_BOAT,new Vec3(10.5,3,2));far.setNoGravity(true);second[0]=far;}
                if(n==470)h.assertFalse(pig.isPassenger(),"a boat 8.5 blocks away (beyond the 4-block range) does not capture");
                if(n==480){
                    second[0].discard();CombatBridge.clear();Passthrough.active=Passthrough.minecraftAuthority=false;h.succeed();
                }
            });
        }
    }
}
