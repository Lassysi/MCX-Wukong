package dev.rehan.passthrough.client;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.WorldBridge;
import dev.rehan.passthrough.HostTerrainTarget;
import net.minecraft.client.Minecraft;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.world.level.ClipContext;
import net.minecraft.world.phys.BlockHitResult;
import net.minecraft.world.phys.EntityHitResult;
import net.minecraft.world.phys.HitResult;
import net.minecraft.world.phys.Vec3;

/** Validate native rays and expose one short-lived surface to vanilla pick/item consumers. */
final class HostRayDiagnostics {
    private record View(long frame,long tick,long created,String world,String epoch,Vec3 eye,Vec3 direction,
                        double reach,float yaw,float pitch,JsonObject renderedPick) {}
    private static final View[] views=new View[128];
    private static View latest,lastView;
    private static JsonObject last;
    private static long accepted,rejected;
    private static String lastReject="";

    static double reach(Minecraft mc){
        if(mc.player==null)return 0;
        double range=mc.player.blockInteractionRange();
        return Double.isFinite(range)?Math.clamp(range,0,12):0;
    }
    static void remember(long frame,HostState.Pose pose){
        var mc=Minecraft.getInstance();
        if(!Passthrough.minecraftAuthority||!WorldBridge.groundCommitted||mc.player==null||mc.screen!=null){clear();return;}
        var eye=new Vec3(pose.x(),pose.y(),pose.z());
        latest=new View(frame,mc.player.tickCount,System.nanoTime(),WorldBridge.worldId,WorldBridge.groundEpoch,
            eye,Vec3.directionFromRotation(pose.pitch(),pose.yaw()).normalize(),reach(mc),pose.yaw(),pose.pitch(),describe(mc.hitResult,eye));
        views[(int)(frame%views.length)]=latest;
    }
    static void clear(){
        HostTerrainTarget.clear();
        if(latest==null&&last==null)return;
        java.util.Arrays.fill(views,null);latest=lastView=null;last=null;
    }
    private static void reject(String reason){rejected++;lastReject=reason;}
    static void accept(JsonObject message){
        // Messages run on the client thread. Contain invalid diagnostics here:
        // an observational packet must never crash gameplay's executor.
        try{
            var mc=Minecraft.getInstance();
            if(!Passthrough.minecraftAuthority||!WorldBridge.groundCommitted||mc.player==null||mc.level==null||mc.screen!=null){reject("inactive");return;}
            long frame=message.get("f").getAsLong();
            if(frame<1){reject("frame");return;}
            View view=views[(int)(frame%views.length)];
            if(view==null||view.frame()!=frame){reject("unknown_frame");return;}
            if(!view.epoch().equals(WorldBridge.groundEpoch)||!view.world().equals(WorldBridge.worldId)
                ||!view.epoch().equals(message.get("epoch").getAsString())||!view.world().equals(message.get("world").getAsString())){reject("world_epoch");return;}
            long age=System.nanoTime()-view.created();
            if(age<0||age>500_000_000L){reject("expired");return;}
            if(lastView!=null&&frame<=lastView.frame()){reject("out_of_order");return;}
            Vec3 eye=vector(message,"eye"),direction=vector(message,"direction");
            double range=message.get("reach").getAsDouble();
            if(message.get("tick").getAsLong()!=view.tick()||!Double.isFinite(range)||range<=0||range>12
                ||Math.abs(range-view.reach())>.0001||eye.distanceToSqr(view.eye())>1e-8||direction.distanceToSqr(view.direction())>1e-8){reject("view_mismatch");return;}
            JsonObject simple=sample(message.getAsJsonObject("simple"),view),complex=sample(message.getAsJsonObject("complex"),view);
            if(simple==null||complex==null){reject("geometry");return;}
            Vec3 end=view.eye().add(view.direction().scale(view.reach()));
            var selection=mc.level.clip(new ClipContext(view.eye(),end,ClipContext.Block.OUTLINE,ClipContext.Fluid.NONE,mc.player));
            var collision=mc.level.clip(new ClipContext(view.eye(),end,ClipContext.Block.COLLIDER,ClipContext.Fluid.NONE,mc.player));
            var result=new JsonObject();result.addProperty("frame",frame);result.addProperty("tick",view.tick());
            result.addProperty("epoch",view.epoch());result.addProperty("world",view.world());
            result.add("eye",array(view.eye()));result.add("direction",array(view.direction()));result.addProperty("reach",range);
            result.add("simple",simple);result.add("complex",complex);
            result.add("mcSelection",describe(selection,view.eye()));result.add("mcCollision",describe(collision,view.eye()));
            result.add("renderedPick",view.renderedPick());
            result.add("simpleComparison",compare(simple,selection,view));result.add("complexComparison",compare(complex,selection,view));
            HostTerrainTarget.offer(mc.player,view.world(),view.epoch(),view.frame(),view.created(),view.eye(),view.direction(),view.reach(),
                complex.get("hit").getAsBoolean()&&complex.get("valid").getAsBoolean(),vector(complex,"point"),vector(complex,"normal"),
                complex.has("support")?complex.get("support").getAsDouble():Double.NaN);
            result.addProperty("observationalOnly",false);last=result;lastView=view;accepted++;
        }catch(RuntimeException e){reject("malformed");}
    }
    private static JsonObject sample(JsonObject input,View view){
        if(input==null)return null;
        boolean hit=input.get("hit").getAsBoolean(),penetrating=input.get("penetrating").getAsBoolean(),valid=input.get("valid").getAsBoolean();
        Vec3 point=vector(input,"point"),normal=vector(input,"normal"),delta=point.subtract(view.eye());
        double distance=input.get("distance").getAsDouble(),along=delta.dot(view.direction());
        if(!Double.isFinite(distance)||Math.abs(distance-delta.length())>.002||along<-.02||along>view.reach()+.02
            ||delta.subtract(view.direction().scale(along)).lengthSqr()>.0009)return null;
        if(!hit&&(penetrating||!valid||Math.abs(distance-view.reach())>.002||normal.lengthSqr()>1e-8))return null;
        if(hit&&valid&&(penetrating||Math.abs(normal.length()-1)>.002))return null;
        if(hit&&!valid&&!penetrating&&normal.length()>.9&&normal.length()<1.1)return null;
        return input.deepCopy();
    }
    private static JsonObject compare(JsonObject nativeHit,BlockHitResult selection,View view){
        var out=new JsonObject();
        double nativeDistance=nativeHit.get("hit").getAsBoolean()&&nativeHit.get("valid").getAsBoolean()?nativeHit.get("distance").getAsDouble():Double.POSITIVE_INFINITY;
        boolean block=selection.getType()==HitResult.Type.BLOCK;
        double blockDistance=block?selection.getLocation().distanceTo(view.eye()):Double.POSITIVE_INFINITY;
        boolean realBlock=block&&!WorldBridge.isHostProxy(selection.getBlockPos());
        double entityDistance=Double.POSITIVE_INFINITY;
        if(view.renderedPick().get("type").getAsString().equals("ENTITY"))entityDistance=view.renderedPick().get("distance").getAsDouble();
        // This comparison stays diagnostic; GameRenderer uses HostTerrainTarget
        // and retains vanilla entity picking after the block/terrain comparison.
        String nearest="miss";
        double closest=nativeDistance;
        if(Double.isFinite(nativeDistance))nearest="native";
        if(realBlock&&blockDistance<=closest+.002){nearest="mc_block";closest=blockDistance;}
        if(entityDistance<=closest+.002&&Double.isFinite(entityDistance))nearest="mc_entity";
        out.addProperty("nearest",nearest);out.addProperty("mcSelectionProxy",block&&!realBlock);
        out.addProperty("nativeValid",nativeHit.get("valid").getAsBoolean());
        if(Double.isFinite(nativeDistance)&&Double.isFinite(blockDistance))out.addProperty("distanceDelta",blockDistance-nativeDistance);
        return out;
    }
    static JsonObject report(){
        var out=new JsonObject();out.addProperty("accepted",accepted);out.addProperty("rejected",rejected);out.addProperty("lastReject",lastReject);
        out.addProperty("observationalOnly",false);out.add("targetUse",HostTerrainTarget.report());
        if(latest!=null){var v=new JsonObject();v.addProperty("frame",latest.frame());v.addProperty("tick",latest.tick());v.addProperty("epoch",latest.epoch());v.addProperty("world",latest.world());
            v.add("eye",array(latest.eye()));v.add("direction",array(latest.direction()));v.addProperty("reach",latest.reach());
            v.addProperty("yaw",latest.yaw());v.addProperty("pitch",latest.pitch());out.add("view",v);}
        if(last!=null&&lastView!=null){
            var copy=last.deepCopy();long age=System.nanoTime()-lastView.created();copy.addProperty("ageMs",age/1e6);
            boolean active=Passthrough.minecraftAuthority&&WorldBridge.groundCommitted&&Minecraft.getInstance().screen==null
                &&lastView.epoch().equals(WorldBridge.groundEpoch)&&lastView.world().equals(WorldBridge.worldId);
            boolean matches=latest!=null&&latest.eye().distanceToSqr(lastView.eye())<=.0625&&latest.direction().dot(lastView.direction())>=.9986295348;
            copy.addProperty("currentViewMatches",matches);copy.addProperty("fresh",active&&matches&&age>=0&&age<=350_000_000L);
            out.add("last",copy);
        }
        return out;
    }
    private static Vec3 vector(JsonObject object,String key){
        JsonArray a=object.getAsJsonArray(key);if(a==null||a.size()!=3)throw new IllegalArgumentException();
        var v=new Vec3(a.get(0).getAsDouble(),a.get(1).getAsDouble(),a.get(2).getAsDouble());
        if(!Double.isFinite(v.x)||!Double.isFinite(v.y)||!Double.isFinite(v.z))throw new IllegalArgumentException();return v;
    }
    private static JsonArray array(Vec3 v){var out=new JsonArray();out.add(v.x);out.add(v.y);out.add(v.z);return out;}
    private static JsonObject describe(HitResult hit,Vec3 eye){
        var out=new JsonObject();out.addProperty("type",hit==null?"NONE":hit.getType().name());
        if(hit==null)return out;
        out.add("point",array(hit.getLocation()));out.addProperty("distance",hit.getLocation().distanceTo(eye));
        if(hit instanceof BlockHitResult block){var pos=block.getBlockPos();var at=new JsonArray();at.add(pos.getX());at.add(pos.getY());at.add(pos.getZ());out.add("block",at);
            out.addProperty("proxy",WorldBridge.isHostProxy(pos));out.addProperty("face",block.getDirection().getName());
            var mc=Minecraft.getInstance();if(mc.level!=null)out.addProperty("id",BuiltInRegistries.BLOCK.getKey(mc.level.getBlockState(pos).getBlock()).toString());}
        if(hit instanceof EntityHitResult entity)out.addProperty("entity",entity.getEntity().getUUID().toString());
        return out;
    }
}
