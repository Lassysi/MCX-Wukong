using System;
using System.Collections.Generic;
using DqjCombat;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    private static bool Covered(double[] p,IList<FVector> boxes,float margin=0){
        foreach(var b in boxes)if(Math.Abs(p[0]-b.X)<=BodySegments.Radius-margin&&Math.Abs(p[1]-b.Y)<=BodySegments.Radius-margin&&Math.Abs(p[2]-b.Z)<=BodySegments.Half-margin)return true;
        return false;
    }
    private static int Main()
    {
        Check(BodySegments.BaseId("139639#3")=="139639"&&BodySegments.BaseId("139639")=="139639"&&BodySegments.BaseId("player")=="player","parts map to one parent enemy");
        Check(!BodySegments.IsLong(495,527,419)&&BodySegments.IsLong(842,2261,720),"ordinary body excluded; measured dragon bounds included");
        Check(BodySegments.KnownDragon("Unit_LYS_KJLDragon_C")&&!BodySegments.KnownDragon("Unit_Normal_C"),"known dragon family retained while coiled");
        Check(BodySegments.PartCount(842,2261,720)==20&&BodySegments.PartCount(1100,300,300)==10&&BodySegments.PartCount(9000,1,1)==24,"dense part counts bounded at 24");
        Check(BodySegments.Radius==500&&BodySegments.Half==500,"10 metre width and height");
        var bones=new List<FVector>();
        for(int i=0;i<91;i++)bones.Add(new FVector(i*50,(float)Math.Sin(i/10.0)*400,(float)Math.Cos(i/20.0)*300));
        for(int i=0;i<30;i++)bones.Add(new FVector(4500+i%3,i%5,0));
        bones.Add(new FVector(0,float.NaN,0));bones.Add(new FVector(0,0,float.PositiveInfinity));
        var mesh=new USkeletalMeshComponent{Bones=bones};var actor=new ACharacter{Mesh=mesh,ClassName="Unit_LYS_KJLDragon_C"};
        var parts=BodySegments.Points("dragon",actor,842,2261,720);
        Check(parts.Count==24,"dragon gets 24 distinct body parts");
        bool all=true,withMargin=true;
        for(int j=0;j<=900;j++){
            double i=j/10.0;var p=new double[]{i*50,Math.Sin(i/10.0)*400,Math.Cos(i/20.0)*300};
            all&=Covered(p,parts);withMargin&=Covered(p,parts,200);
        }
        Check(all&&withMargin,"continuous 45 m curved body, head and tail covered with 2 m overlap margin");
        bool finite=true;foreach(var p in parts)finite&=!float.IsNaN(p.X+p.Y+p.Z)&&!float.IsInfinity(p.X+p.Y+p.Z);
        Check(finite,"NaN Y and infinite Z bones rejected");
        bones[0]=new FVector(-200,200,100);var moved=BodySegments.Points("dragon",actor,842,2261,720);
        Check(moved.Exists(p=>p.X==-200&&p.Y==200),"chosen bones follow moving pose every frame");
        Check(BodySegments.Points("coiled",actor,100,100,100).Count==24,"known dragon keeps hitboxes even in compact pose");
        Check(BodySegments.Points("ordinary",new ACharacter{Mesh=mesh},200,200,200).Count==0,"ordinary actors do not gain giant aiming boxes");
        Check(BodySegments.Points("later-boss",new ACharacter{Mesh=mesh,ClassName="Unit_UnknownFutureBoss_C"},200,200,200,true).Count>=20,"unknown later Boss discovered by skeleton even with small reported bounds");
        var bulkyBones=new List<FVector>();for(int i=0;i<20;i++)bulkyBones.Add(new FVector(i%3*250,i%5*200,i*50));
        var bulky=new ACharacter{Mesh=new USkeletalMeshComponent{Bones=bulkyBones}};
        var bulkyParts=BodySegments.Points("bulky",bulky,600,600,600);
        bool bulkyCovered=true;foreach(var b in bulkyBones)bulkyCovered&=Covered(new double[]{b.X,b.Y,b.Z},bulkyParts,100);
        Check(bulkyParts.Count>=6&&bulkyCovered,"unknown broad, tall giant gains complete body coverage");
        var smallRig=new ACharacter{Mesh=new USkeletalMeshComponent{Bones=new List<FVector>{new FVector(0,0,0),new FVector(0,0,200)}}};
        Check(BodySegments.Points("spell-bounds",smallRig,3000,3000,3000,true).Count==0,"large attached FX bounds do not turn a small skeleton into a giant");
        Check(BodySegments.LargeSkeleton(450,100,100,true)&&!BodySegments.LargeSkeleton(450,100,100,false),"Boss and ordinary-enemy skeleton thresholds distinguished");
        var fallback=new ACharacter{ClassName="Unit_LYS_KJLDragon_C",Centre=new FVector(1000,2000,3000),Extent=new FVector(2261,200,300)};
        var line=BodySegments.Points("fallback",fallback,2261,200,300);bool covered=true;
        for(int i=0;i<=100;i++)covered&=Covered(new double[]{1000-2261+4522.0*i/100,2000,3000},line);
        Check(line.Count==20&&covered,"missing skeleton falls back to overlapping bounds-axis boxes");
        fallback.Extent=new FVector(99999,0,0);Check(BodySegments.Points("bad",fallback,99999,0,0).Count==0,"corrupt giant bounds rejected");
        Check(BodySegments.Spread(new List<double[]>{new double[]{0,0,0},new double[]{0,0,0}},24).Count==1,"duplicate bones do not waste network parts");
        Check(BodySegments.Spread(new List<double[]>(),24).Count==0,"empty bone input safe");
        BodySegments.Forget();Check(BodySegments.Points("dragon",actor,842,2261,720).Count==24,"world cleanup permits fresh body sampling");
        Console.WriteLine("BODY SEGMENTS PASS checks="+checks);return 0;
    }
}
namespace DqjCombat{internal static class Log{internal static void Write(string c,string m){Console.WriteLine(c+" | "+m);}}}
namespace UnrealEngine.Runtime{public struct FName{public string N;public FName(string n){N=n;}public override string ToString(){return N;}}}
namespace UnrealEngine.Engine{
    public struct FVector{public float X,Y,Z;public FVector(float x,float y,float z){X=x;Y=y;Z=z;}}
    public class UClass{public string Name;public string GetName(){return Name;}}
    public class USkeletalMeshComponent{
        public bool IsDestroyed;public List<FVector> Bones=new List<FVector>();
        public int GetNumBones(){return Bones.Count;}
        public FName GetBoneName(int i){return new FName(i.ToString());}
        public FVector GetSocketLocation(FName n){return Bones[int.Parse(n.N)];}
    }
    public class ACharacter{
        public USkeletalMeshComponent Mesh;public string ClassName="Unit_Normal_C";public FVector Centre,Extent;
        public UClass GetClass(){return new UClass{Name=ClassName};}
        public void GetActorBounds(bool only,out FVector centre,out FVector extent,bool children){centre=Centre;extent=Extent;}
    }
}
