package dev.rehan.passthrough.client;

import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import dev.rehan.passthrough.client.mixin.EntityCollideInvoker;
import java.util.List;
import net.minecraft.client.Minecraft;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.phys.AABB;
import net.minecraft.world.phys.Vec3;

/** BUGS-OPEN 03 probe: what vanilla collision actually returns around the player.
 *  Read-only: computes collisions and candidate moves, never moves the player. */
final class CollisionProbe {
    private CollisionProbe() {}
    private static JsonArray box(AABB b){
        var a=new JsonArray();a.add(b.minX);a.add(b.minY);a.add(b.minZ);a.add(b.maxX);a.add(b.maxY);a.add(b.maxZ);return a;
    }
    private static JsonArray vec(Vec3 v){var a=new JsonArray();a.add(v.x);a.add(v.y);a.add(v.z);return a;}
    static JsonObject probe(Minecraft mc){
        var out=new JsonObject();var player=mc.player;
        if(player==null||mc.level==null){out.addProperty("error","no player");return out;}
        AABB body=player.getBoundingBox();out.add("box",box(body));out.addProperty("maxUpStep",player.maxUpStep());
        var shapes=new JsonArray();int overlapping=0,count=0;
        for(var shape:mc.level.getBlockCollisions(player,body.inflate(1.0,0.6,1.0))){
            for(var part:shape.toAabbs()){
                if(part.intersects(body))overlapping++;
                if(count++<48)shapes.add(box(part));
            }
        }
        out.add("nearShapes",shapes);out.addProperty("nearShapeCount",count);out.addProperty("overlappingNow",overlapping);
        var moves=new JsonObject();
        double[][] dirs={{.5,0,0},{-.5,0,0},{0,0,.5},{0,0,-.5}};String[] names={"+x","-x","+z","-z"};
        for(int i=0;i<4;i++){
            Vec3 m=new Vec3(dirs[i][0],-0.08,dirs[i][2]);var r=new JsonObject();
            r.add("boundingOnly",vec(Entity.collideBoundingBox(player,m,body,mc.level,List.of())));
            r.add("withStepUp",vec(((EntityCollideInvoker)player).dqj$collide(m)));
            moves.add(names[i],r);
        }
        out.add("moves",moves);
        return out;
    }
}
