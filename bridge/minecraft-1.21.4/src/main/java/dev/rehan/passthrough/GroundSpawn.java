package dev.rehan.passthrough;

import java.util.ArrayList;
import java.util.Comparator;
import net.minecraft.world.level.Level;
import net.minecraft.world.phys.AABB;

/** Resolve a host floor point against MC's actual body and collision shapes. */
public final class GroundSpawn {
    private GroundSpawn(){}
    static AABB body(double x,double y,double z){return new AABB(x-.299,y+.001,z-.299,x+.299,y+1.799,z+.299);}
    public static boolean supported(Level level,double x,double y,double z){
        var body=body(x,y,z);
        return !level.getBlockCollisions(null,body).iterator().hasNext()
            &&level.getBlockCollisions(null,body.move(0,-.04,0)).iterator().hasNext();
    }
    static double resolve(Level level,double x,double y,double z){
        // Never shift X/Z or change the persistent world anchor. A native point
        // may be below the top of a neighbouring support tile under the MC body.
        var candidates=new ArrayList<Double>();candidates.add(y);
        // Live97 shrine CG return: native floor Y=63.818198 intersects an existing, unregistered
        // oak slab at (5,64,4), top=64.5. Match the bridge's 1.26 m vanilla step envelope.
        // Only choose a collision-checked top; never delete the old block or shift the world anchor.
        var search=body(x,y,z).inflate(0,1.3,0);
        for(var shape:level.getBlockCollisions(null,search))for(var part:shape.toAabbs()){
            double top=part.maxY;
            if(top>=y-.6&&top<=y+1.26&&top>=-50&&top<=300)candidates.add(top);
        }
        HostBlockAnchors.addStandingCandidates(level,x,y,z,candidates);
        candidates.sort(Comparator.comparingDouble(top->Math.abs(top-y)));
        for(double top:candidates)if(supported(level,x,top,z))return top;
        return Double.NaN;
    }
    /** Preview99 (user: respawn stuck when MC blocks fill the native spawn point): the exact column first;
     *  if nothing there fits the body, the nearest free column within 1.5 m (8 directions, 0.6/1.0/1.5 m).
     *  Only the player moves; the world anchor and every block stay as they are. Returns {x,y,z} or null. */
    static double[] resolveNear(Level level,double x,double y,double z){
        double top=resolve(level,x,y,z);
        if(Double.isFinite(top))return new double[]{x,top,z};
        for(double r:new double[]{.6,1.0,1.5})for(int i=0;i<8;i++){
            double a=i*Math.PI/4,nx=x+Math.cos(a)*r,nz=z+Math.sin(a)*r;
            top=resolve(level,nx,y,nz);
            if(Double.isFinite(top))return new double[]{nx,top,nz};
        }
        return null;
    }
}
