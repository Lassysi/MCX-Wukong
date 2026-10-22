using System;
using System.Collections.Generic;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // Preview120 (user: Kang-Jin Loong, a 45 m flying dragon that never stops moving, could not be hit at all): one MC
    // proxy per enemy cannot cover a long body. Its bounds (8.4 x 22.6 x 7.2 m) tripped the "bounds swollen" guard, so
    // only the 1 m capsule at the actor root was hittable. Long bodies now get extra proxies spread along their own
    // skeleton ("id#k"); damage and effects on a part go to the parent enemy (BaseId).
    internal static class BodySegments
    {
        private static readonly float LongExtent=1000f;   // half-extent (cm) above which a body counts as long
        // User explicitly wants generous coverage. Each part is a 10 m cube, rather than a 4.4 m cube.
        private static readonly float PartRadius=500f,PartHalf=500f;
        internal static float Radius{get{return PartRadius;}}
        internal static float Half{get{return PartHalf;}}

        internal static string BaseId(string id){if(id==null)return null;int i=id.IndexOf('#');return i<0?id:id.Substring(0,i);}
        internal static bool IsLong(float ex,float ey,float ez){return Math.Max(ex,Math.Max(ey,ez))>LongExtent;}
        internal static bool KnownDragon(string className){return className!=null&&className.IndexOf("KJLDragon",StringComparison.OrdinalIgnoreCase)>=0;}
        internal static bool LargeSkeleton(float x,float y,float z,bool boss){return Math.Max(x,Math.Max(y,z))>=(boss?400f:600f);}
        /// Dense coverage with overlap: one centre per 2.5 m of body, capped at 24.
        internal static int PartCount(float ex,float ey,float ez)
        {
            double longest=Math.Max(ex,Math.Max(ey,ez))*2;
            return Math.Max(6,Math.Min(24,(int)Math.Ceiling(longest/250.0)+1));
        }
        /// Farthest-point sampling (pure, tested): k well-spread indices, starting at the point farthest from the centroid.
        internal static List<int> Spread(IList<double[]> pts,int k)
        {
            var chosen=new List<int>();int n=pts.Count;if(n==0||k<=0)return chosen;
            double cx=0,cy=0,cz=0;foreach(var p in pts){cx+=p[0];cy+=p[1];cz+=p[2];}cx/=n;cy/=n;cz/=n;
            int first=0;double best=-1;
            for(int i=0;i<n;i++){double d=Sq(pts[i],cx,cy,cz);if(d>best){best=d;first=i;}}
            chosen.Add(first);var near=new double[n];
            for(int i=0;i<n;i++)near[i]=Sq(pts[i],pts[first][0],pts[first][1],pts[first][2]);
            while(chosen.Count<Math.Min(k,n)){
                int next=-1;best=-1;
                for(int i=0;i<n;i++)if(near[i]>best){best=near[i];next=i;}
                if(best<=1)break;   // all remaining bones sit on chosen ones
                chosen.Add(next);
                for(int i=0;i<n;i++){double d=Sq(pts[i],pts[next][0],pts[next][1],pts[next][2]);if(d<near[i])near[i]=d;}
            }
            return chosen;
        }
        private static double Sq(double[] p,double x,double y,double z){double dx=p[0]-x,dy=p[1]-y,dz=p[2]-z;return dx*dx+dy*dy+dz*dz;}

        // ---- live bone sampling (bone choice refreshed every second, positions read every frame)
        private sealed class Choice{internal List<FName> Bones=new List<FName>();internal int At;}
        private static readonly Dictionary<string,Choice> choices=new Dictionary<string,Choice>();
        private static readonly HashSet<string> logged=new HashSet<string>();
        internal static void Forget(){choices.Clear();logged.Clear();}
        /// World positions of the chosen body bones, or an empty list when the body is not long or has no skeleton.
        internal static List<FVector> Points(string id,ACharacter actor,float ex,float ey,float ez,bool boss=false,float capsuleRadius=0,float capsuleHalf=0)
        {
            var result=new List<FVector>();
            bool dragon=KnownDragon(actor.GetClass().GetName());
            if(!dragon&&!boss&&!IsLong(ex,ey,ez)&&Math.Max(ex,Math.Max(ey,ez))<500&&capsuleHalf<250&&capsuleRadius<150)return result;
            bool fallback=dragon||(capsuleHalf>=300||capsuleRadius>=150)&&Math.Max(ex,Math.Max(ey,ez))>=300;
            USkeletalMeshComponent mesh=null;try{mesh=actor.Mesh;}catch{}
            if(mesh==null||mesh.IsDestroyed)return fallback?Fallback(actor,ex,ey,ez):result;
            int now=Environment.TickCount;Choice c;
            if(!choices.TryGetValue(id,out c)||unchecked(now-c.At)>1000){
                c=new Choice{At=now};
                try{
                    int count=mesh.GetNumBones();var names=new List<FName>();var pts=new List<double[]>();
                    for(int i=0;i<count;i++){var name=mesh.GetBoneName(i);var at=mesh.GetSocketLocation(name);
                        if(!Finite(at.X)||!Finite(at.Y)||!Finite(at.Z))continue;names.Add(name);pts.Add(new double[]{at.X,at.Y,at.Z});}
                    float sx=0,sy=0,sz=0;
                    if(pts.Count>0){
                        double minX=pts[0][0],maxX=minX,minY=pts[0][1],maxY=minY,minZ=pts[0][2],maxZ=minZ;
                        foreach(var p in pts){minX=Math.Min(minX,p[0]);maxX=Math.Max(maxX,p[0]);minY=Math.Min(minY,p[1]);maxY=Math.Max(maxY,p[1]);minZ=Math.Min(minZ,p[2]);maxZ=Math.Max(maxZ,p[2]);}
                        sx=(float)(maxX-minX);sy=(float)(maxY-minY);sz=(float)(maxZ-minZ);
                    }
                    // Skeleton span prevents giant attached spell effects alone from qualifying a small enemy.
                    if(dragon||LargeSkeleton(sx,sy,sz,boss))
                        foreach(int i in Spread(pts,dragon?24:PartCount(sx/2,sy/2,sz/2)))c.Bones.Add(names[i]);
                    if(c.Bones.Count>0&&logged.Add(id))Log.Write("target-size","id="+id+" large skeleton span="+(int)sx+","+(int)sy+","+(int)sz+"cm: "+c.Bones.Count+" extra hitboxes, width=10m height=10m, along "+count+" bones");
                }catch(Exception e){if(logged.Add(id+"!"))Log.Write("target-size","id="+id+" body segments unavailable: "+e.GetType().Name);}
                choices[id]=c;
            }
            foreach(var bone in c.Bones){try{var p=mesh.GetSocketLocation(bone);if(Finite(p.X)&&Finite(p.Y)&&Finite(p.Z))result.Add(p);}catch{}}
            return result.Count==0&&fallback?Fallback(actor,ex,ey,ez):result;
        }
        private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        private static List<FVector> Fallback(ACharacter actor,float ex,float ey,float ez)
        {
            // Missing skeleton data must not collapse this known dragon back to its 1 m root capsule.
            var points=new List<FVector>();
            try{
                FVector centre,extent;actor.GetActorBounds(false,out centre,out extent,false);
                if(!Finite(centre.X+centre.Y+centre.Z+extent.X+extent.Y+extent.Z)||Math.Max(extent.X,Math.Max(extent.Y,extent.Z))>10000)return points;
                int axis=extent.X>=extent.Y&&extent.X>=extent.Z?0:extent.Y>=extent.Z?1:2;int n=PartCount(ex,ey,ez);
                float length=axis==0?extent.X:axis==1?extent.Y:extent.Z;
                for(int i=0;i<n;i++){
                    float d=-length+2*length*i/(n-1);
                    points.Add(new FVector(centre.X+(axis==0?d:0),centre.Y+(axis==1?d:0),centre.Z+(axis==2?d:0)));
                }
            }catch{}
            return points;
        }
    }
}
