package dev.rehan.passthrough;

import com.google.gson.JsonObject;
import net.minecraft.core.BlockPos;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.block.Blocks;
import net.minecraft.world.phys.Vec3;

/** Await measured host support before entering an unstreamed column. */
public final class TerrainLease {
    public static volatile boolean enabled,waiting;
    public static volatile long waits,requests;
    private static long lastRequest;
    private TerrainLease(){}
    private static boolean supported(Entity player,int x,int z){
        var level=player.level();var measured=WorldBridge.supportAt(x,z);
        // Server registration precedes the client block packet. Both the shape
        // and the local block must exist before the client may consume support.
        if(measured!=null&&!level.getBlockState(measured).getCollisionShape(level,measured).isEmpty())return true;
        // A real player-built bridge remains walkable outside measured terrain.
        int top=(int)Math.floor(player.getY()+.6),bottom=(int)Math.floor(player.getY()-1.5);
        for(int y=bottom;y<=top;y++){
            var p=new BlockPos(x,y,z);var state=level.getBlockState(p);
            if(!state.is(Blocks.BARRIER)&&!state.getCollisionShape(level,p).isEmpty())return true;
        }
        return false;
    }
    private static boolean covered(Entity player,double dx,double dz){
        var box=player.getBoundingBox().move(dx,0,dz);
        int x0=(int)Math.floor(box.minX+1e-5),x1=(int)Math.floor(box.maxX-1e-5);
        int z0=(int)Math.floor(box.minZ+1e-5),z1=(int)Math.floor(box.maxZ-1e-5);
        for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)if(!supported(player,x,z))return false;
        return true;
    }
    private static boolean route(Entity player,Vec3 delta,double fraction){
        int steps=Math.max(1,(int)Math.ceil(Math.max(Math.abs(delta.x),Math.abs(delta.z))*fraction/.25));
        if(steps>128)return false;
        for(int i=1;i<=steps;i++)if(!covered(player,delta.x*fraction*i/steps,delta.z*fraction*i/steps))return false;
        return true;
    }
    private static void request(Entity player,Vec3 delta){request(player,delta,1.5,false);}
    private static void request(Entity player,Vec3 delta,double ahead,boolean deep){
        if(!player.level().isClientSide)return;
        long now=System.nanoTime();if(now-lastRequest<150_000_000L)return;lastRequest=now;requests++;
        double length=Math.hypot(delta.x,delta.z),scale=length>1e-8?ahead/length:0;
        JsonObject message=new JsonObject();message.addProperty("t","terrain_wait");message.addProperty("epoch",WorldBridge.groundEpoch);
        message.addProperty("x",player.getX()+delta.x*scale);message.addProperty("y",player.getY());message.addProperty("z",player.getZ()+delta.z*scale);
        if(deep)message.addProperty("deep",true);
        Passthrough.events.accept(message.toString());
    }
    /** Preview90 (user: keep elytra + firework flight): gliding or creative flight is not held at unmeasured
     *  columns (that guard is for walking off into the MC void). Native ground is requested ahead along the
     *  flight path, further the faster the flight, and measured up to 40 m below, so landing finds support. */
    private static boolean flying(Entity player){
        return player instanceof Player p&&(p.isFallFlying()||p.getAbilities().flying);
    }
    public static Vec3 beforeMove(Entity player,Vec3 delta){
        if(!enabled||!(player instanceof Player)||player.level().dimension()!=Level.OVERWORLD)return delta;
        // The client may receive the teleport before the destination chunk.
        // Waiting for its support acknowledgement must not let gravity run ahead.
        if(WorldBridge.isPreparing())return Vec3.ZERO;
        if(!WorldBridge.groundCommitted)return delta;
        if(Math.abs(delta.x)+Math.abs(delta.z)<1e-8){if(player.level().isClientSide)waiting=false;return delta;}
        if(flying(player)){
            if(player.level().isClientSide){waiting=false;request(player,delta,Math.min(10,1.5+Math.hypot(delta.x,delta.z)*8),true);}
            return delta;
        }
        boolean ready=route(player,delta,1);
        if(player.level().isClientSide)waiting=!ready;
        if(ready){
            double length=Math.hypot(delta.x,delta.z);
            if(!covered(player,delta.x*1.5/length,delta.z*1.5/length))request(player,delta);
            return delta;
        }
        if(player.level().isClientSide)waits++;
        request(player,delta);
        double lo=0,hi=1;
        for(int i=0;i<10;i++){double mid=(lo+hi)*.5;if(route(player,delta,mid))lo=mid;else hi=mid;}
        // Leave vertical velocity and all vanilla acceleration/friction intact.
        return new Vec3(delta.x*lo,delta.y,delta.z*lo);
    }
}
