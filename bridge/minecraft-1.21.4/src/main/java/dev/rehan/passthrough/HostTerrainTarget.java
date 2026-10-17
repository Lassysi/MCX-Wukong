package dev.rehan.passthrough;

import com.google.gson.JsonObject;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicLong;
import java.util.function.Supplier;
import net.minecraft.core.BlockPos;
import net.minecraft.core.Direction;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.item.context.UseOnContext;
import net.minecraft.world.item.context.BlockPlaceContext;
import net.minecraft.world.level.ClipContext;
import net.minecraft.world.level.Level;
import net.minecraft.world.level.block.Blocks;
import net.minecraft.world.phys.BlockHitResult;
import net.minecraft.world.phys.HitResult;
import net.minecraft.world.phys.Vec3;

/** A frame-validated native surface; vanilla still owns item use, placement and consumption. */
public final class HostTerrainTarget {
    private record Target(UUID player,String world,String epoch,long frame,long created,Vec3 eye,Vec3 direction,
                          double reach,Vec3 point,Vec3 normal,BlockPos block,Direction face,double support) {}
    private static volatile Target target;
    private static volatile BlockPos lastSurface;
    private static final ThreadLocal<Boolean> ignoring=ThreadLocal.withInitial(()->false);
    private static final AtomicLong selected=new AtomicLong(),clientAllowed=new AtomicLong(),serverAllowed=new AtomicLong(),denied=new AtomicLong();
    private HostTerrainTarget() {}

    public static void clear(){target=null;}
    public static boolean ignoringProxies(){return ignoring.get();}
    private static <T> T withoutProxies(Supplier<T> action){
        boolean old=ignoring.get();ignoring.set(true);
        try{return action.get();}finally{if(old)ignoring.set(true);else ignoring.remove();}
    }
    public static void offer(Player player,String world,String epoch,long frame,long created,Vec3 eye,Vec3 direction,
                             double reach,boolean validHit,Vec3 point,Vec3 normal,double support){
        if(!validHit){clear();return;}
        var face=Direction.getApproximateNearest(normal);
        var block=BlockPos.containing(point.subtract(normal.scale(.001)));
        target=new Target(player.getUUID(),world,epoch,frame,created,eye,direction,reach,point,normal,block,face,support);
        // Preview84 (user: blocks often cannot be placed): use on native ground is only valid on a host
        // surface proxy, and the bounded raster around the player often has none at the aimed cell.
        // Aimed ground (<= 35 deg) gets its surface proxy on demand, so vanilla placement and the
        // root-height anchor run exactly as on rastered ground.
        if(normal.y>=MIN_UP&&!block.equals(lastSurface)){lastSurface=block;WorldBridge.ensureSurface(block,point.y-block.getY(),epoch);}
    }
    private static boolean enabled(Entity entity){
        return Passthrough.minecraftAuthority&&WorldBridge.groundCommitted&&entity instanceof Player&&entity.isAlive()
            &&entity.level().dimension()==Level.OVERWORLD;
    }
    private static boolean current(Target t,Player player,Vec3 eye,Vec3 direction){
        long age=t==null?-1:System.nanoTime()-t.created();
        return t!=null&&enabled(player)&&t.player().equals(player.getUUID())&&t.world().equals(WorldBridge.worldId)
            &&t.epoch().equals(WorldBridge.groundEpoch)&&age>=0&&age<=250_000_000L
            &&eye.distanceToSqr(t.eye())<=.0625&&direction.dot(t.direction())>=.999847695;
    }
    /** Native ground up to 60 degrees accepts a root block (Preview87; Preview46 35, earlier ~11). */
    private static final double MIN_UP=Math.cos(Math.toRadians(60));
    /** Preview85 root offset {dx,dy,dz} for a block placed against native terrain, or null (grid).
     *  Ground (face up, <= 35 deg): the footprint is centred on the aim point; its bottom rests on the
     *  lowest of the host's footprint traces ("support", native geometry), else on the lowest footprint
     *  corner of the hit plane. Walls: flush with the hit plane, centred on the aim point. */
    static double[] rootOffset(BlockPlaceContext context){
        Player player=context.getPlayer();Target t=target;
        if(player==null||!current(t,player,player.getEyePosition(),player.getViewVector(1).normalize())
            ||t.face()!=context.getClickedFace()||!t.block().relative(t.face()).equals(context.getClickedPos())
            ||t.point().distanceToSqr(context.getClickLocation())>.0004)return null;
        BlockPos pos=context.getClickedPos();Vec3 p=t.point(),n=t.normal();
        double cx=pos.getX()+.5,cy=pos.getY()+.5,cz=pos.getZ()+.5;
        if(t.face()==Direction.UP){
            if(n.y<MIN_UP)return null;
            double dx=Math.max(-.5,Math.min(.5,p.x-cx)),dz=Math.max(-.5,Math.min(.5,p.z-cz));
            double bottom=t.support();
            if(!Double.isFinite(bottom)||bottom>p.y+.001||bottom<p.y-2){
                bottom=Double.POSITIVE_INFINITY;
                for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2){
                    double x=cx+dx+sx*.5,z=cz+dz+sz*.5;
                    bottom=Math.min(bottom,p.y-(n.x*(x-p.x)+n.z*(z-p.z))/n.y);
                }
            }
            double dy=bottom-pos.getY();
            return Double.isFinite(dy)&&dy>=-2&&dy<=.001?new double[]{dx,dy,dz,0}:null;
        }
        if(!t.face().getAxis().isHorizontal())return null;
        double dx=p.x-cx,dy=p.y-cy,dz=p.z-cz;
        if(t.face().getAxis()==Direction.Axis.X)dx=p.x+t.face().getStepX()*.5-cx;else dz=p.z+t.face().getStepZ()*.5-cz;
        if(Math.abs(dx)>1||Math.abs(dz)>1||Math.abs(dy)>.5)return null;
        return new double[]{dx,dy,dz,2};
    }
    static Double rootHeight(BlockPlaceContext context){
        Player player=context.getPlayer();Target t=target;
        if(player==null||!current(t,player,player.getEyePosition(),player.getViewVector(1).normalize())
            ||t.face()!=Direction.UP||t.normal().y<MIN_UP||context.getClickedFace()!=Direction.UP
            ||!t.block().above().equals(context.getClickedPos())
            ||t.point().distanceToSqr(context.getClickLocation())>.0004)return null;
        // The native hit plane through point/normal; the block's bottom rests on its
        // lowest footprint corner, so it never floats (the uphill side is buried).
        BlockPos pos=context.getClickedPos();Vec3 p=t.point(),n=t.normal();double low=Double.POSITIVE_INFINITY;
        for(int cx=0;cx<=1;cx++)for(int cz=0;cz<=1;cz++){
            double x=pos.getX()+cx,z=pos.getZ()+cz;
            low=Math.min(low,p.y-(n.x*(x-p.x)+n.z*(z-p.z))/n.y);
        }
        double dy=low-pos.getY();
        return Double.isFinite(dy)&&dy>=-2&&dy<=.001?dy:null;
    }
    public static HitResult pick(Entity entity,double range,float partial,boolean fluids){
        // Leave custom longer entity reach to the original MC path until native
        // rays carry that range too. Never infer an untested host segment.
        if(!enabled(entity)||range>((Player)entity).blockInteractionRange()+.001||range>12)return entity.pick(range,partial,fluids);
        HitResult real=withoutProxies(()->entity.pick(range,partial,fluids));
        Target t=target;Vec3 eye=entity.getEyePosition(partial),direction=entity.getViewVector(partial).normalize();
        if(!current(t,(Player)entity,eye,direction)){
            // Preview87 (BUG-12: first placement often did nothing): the native ray is about one host frame old,
            // so after a small turn the stored hit is no longer on the crosshair ray. Re-aim on the hit plane
            // (native ground is locally planar) when the new point stays within 0.5 m of the native hit.
            Target moved=reaim(t,(Player)entity,eye,direction,range);
            if(moved==null){miss(t==null?"no-native-target":"stale-target");return real;}
            target=t=moved;
        }
        Vec3 delta=t.point().subtract(eye);double along=delta.dot(direction),distance=delta.length();
        if(along<0||distance>range+.002||delta.subtract(direction.scale(along)).lengthSqr()>.0009){
            Target moved=reaim(t,(Player)entity,eye,direction,range);
            if(moved==null){miss("off-ray");return real;}
            target=t=moved;delta=t.point().subtract(eye);distance=delta.length();
        }
        if(real.getType()!=HitResult.Type.MISS&&real.getLocation().distanceTo(eye)<=distance+.002)return real;
        selected.incrementAndGet();
        return new BlockHitResult(t.point(),t.face(),t.block(),false);
    }
    private static Target reaim(Target t,Player player,Vec3 eye,Vec3 direction,double range){
        if(t==null||!enabled(player)||!t.player().equals(player.getUUID())||!t.world().equals(WorldBridge.worldId)||!t.epoch().equals(WorldBridge.groundEpoch))return null;
        long age=System.nanoTime()-t.created();
        if(age<0||age>1_000_000_000L||eye.distanceToSqr(t.eye())>1)return null;
        double facing=direction.dot(t.normal());if(facing>-.05)return null;
        double s=t.point().subtract(eye).dot(t.normal())/facing;
        if(!(s>0)||s>range+.002)return null;
        Vec3 p=eye.add(direction.scale(s));
        if(p.distanceToSqr(t.point())>.25)return null;
        var block=BlockPos.containing(p.subtract(t.normal().scale(.001)));
        if(t.normal().y>=MIN_UP&&!block.equals(lastSurface)){lastSurface=block;WorldBridge.ensureSurface(block,p.y-block.getY(),t.epoch());}
        reaimed.incrementAndGet();
        return new Target(t.player(),t.world(),t.epoch(),t.frame(),System.nanoTime(),eye,direction,t.reach(),p,t.normal(),block,t.face(),
            Double.isFinite(t.support())&&p.distanceToSqr(t.point())<.01?t.support():Double.NaN);
    }
    private static final AtomicLong reaimed=new AtomicLong();
    private static volatile String lastMiss="";
    private static void miss(String reason){lastMiss=reason;}
    public static String lastMiss(){return lastMiss;}
    public static long reaimedCount(){return reaimed.get();}
    public static boolean allowUse(UseOnContext context){
        Player player=context.getPlayer();
        if(player==null||!enabled(player))return true;
        var state=context.getLevel().getBlockState(context.getClickedPos());
        // A real MC/mod block retains its native item/container handling.
        if(!state.isAir()&&!state.is(Blocks.BARRIER))return true;
        Target t=target;
        boolean proxy=state.is(Blocks.BARRIER)&&WorldBridge.isHostProxy(context.getClickedPos());
        boolean nativeAir=state.isAir()&&t!=null&&t.block().equals(context.getClickedPos());
        if(!proxy&&!nativeAir)return true;
        boolean valid=proxy&&current(t,player,player.getEyePosition(),player.getViewVector(1).normalize())
            &&t.block().equals(context.getClickedPos())&&t.face()==context.getClickedFace()
            &&t.point().distanceToSqr(context.getClickLocation())<=.0004
            &&player.getEyePosition().distanceTo(t.point())<=Math.min(t.reach(),player.blockInteractionRange())+.002;
        if(valid){
            // Recheck real MC blocks on the consuming thread. A new player block
            // in front of the surface must not be skipped by a cached native ray.
            Vec3 eye=player.getEyePosition();
            var obstruction=withoutProxies(()->context.getLevel().clip(new ClipContext(eye,t.point(),ClipContext.Block.OUTLINE,ClipContext.Fluid.NONE,player)));
            valid=obstruction.getType()==HitResult.Type.MISS||obstruction.getLocation().distanceTo(eye)>=t.point().distanceTo(eye)-.002;
        }
        if(valid){if(context.getLevel().isClientSide)clientAllowed.incrementAndGet();else serverAllowed.incrementAndGet();}
        else{denied.incrementAndGet();if(!context.getLevel().isClientSide)Passthrough.LOG.info("DQJ place-diag use refused proxy={} nativeAir={} target={} face={}/{}",
            proxy,nativeAir,t==null?"none":(current(t,player,player.getEyePosition(),player.getViewVector(1).normalize())?"current":"stale"),t==null?"-":t.face(),context.getClickedFace());}
        return valid;
    }
    public static JsonObject report(){
        var out=new JsonObject();out.addProperty("selected",selected.get());out.addProperty("clientUses",clientAllowed.get());
        out.addProperty("serverUses",serverAllowed.get());out.addProperty("deniedUses",denied.get());
        Target t=target;
        if(t!=null){out.addProperty("frame",t.frame());out.addProperty("ageMs",(System.nanoTime()-t.created())/1e6);
            out.addProperty("face",t.face().getName());out.addProperty("mappedProxy",WorldBridge.isHostProxy(t.block()));}
        return out;
    }
}
