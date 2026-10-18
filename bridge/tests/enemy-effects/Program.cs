using System;
using System.Collections.Generic;
using DqjCombat;
using UnrealEngine.Engine;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    private static bool Near(float a,double b){return Math.Abs(a-b)<.001;}
    private static int Main()
    {
        Check(Near(EnemyEffects.Dilation(-1,false,false,false,false),1),"no effect keeps normal time");
        Check(Near(EnemyEffects.Dilation(0,false,false,false,false),.6),"Slowness I -> 0.6 (Live111 stronger)");
        Check(Near(EnemyEffects.Dilation(1,false,false,false,false),.45),"Slowness II -> 0.45");
        Check(Near(EnemyEffects.Dilation(3,false,false,false,false),.3),"Slowness IV floored at 0.3");
        Check(Near(EnemyEffects.Dilation(-1,false,true,false,false),.25),"cobweb -> 0.25");
        Check(Near(EnemyEffects.Dilation(-1,true,false,false,false),.15),"powder snow freeze -> 0.15");
        Check(Near(EnemyEffects.Dilation(-1,false,false,true,false),.5),"soul sand -> 0.5");
        Check(Near(EnemyEffects.Dilation(-1,true,true,false,true),.65),"Boss never below 0.65");
        Check(Near(EnemyEffects.WeaknessScale(-1,false),1),"no weakness keeps damage");
        Check(Near(EnemyEffects.WeaknessScale(0,false),.75),"Weakness I -> x0.75");
        Check(Near(EnemyEffects.WeaknessScale(5,false),.4),"strong weakness floored at x0.4");
        Check(Near(EnemyEffects.WeaknessScale(3,true),.7),"Boss weakness floored at x0.7");
        float x,y,z;EnemyEffects.LaunchVelocity(.4,0,0,out x,out y,out z);
        Check(Near(x,0)&&Near(y,-600)&&Near(z,0),"MC +X knockback -> Unreal -Y 600 cm/s");
        EnemyEffects.LaunchVelocity(0,-1,.4,out x,out y,out z);Check(Near(z,0)&&Near(x,600),"downward MC velocity never pushes into the ground");
        EnemyEffects.LaunchVelocity(5,5,5,out x,out y,out z);Check(Math.Sqrt(x*x+y*y+z*z)<=2500.01,"launch capped at 2500 cm/s");
        var a=new AActor{CustomTimeDilation=1f};
        EnemyEffects.SetDilation(a,.45f);Check(Near(a.CustomTimeDilation,.45),"lease applies dilation");
        EnemyEffects.SetDilation(a,.3f);EnemyEffects.SetDilation(a,1f);Check(Near(a.CustomTimeDilation,1)&&EnemyEffects.Count==0,"effect end restores the original value");
        var b=new AActor{CustomTimeDilation=.9f};EnemyEffects.SetDilation(b,.5f);McPlayerHealthGate.WeakScale["7"]=.75f;
        EnemyEffects.RestoreAll();Check(Near(b.CustomTimeDilation,.9)&&McPlayerHealthGate.WeakScale.Count==0,"link reset restores leases and clears weakness");
        var c=new AActor{CustomTimeDilation=1f};EnemyEffects.SetDilation(c,.3f);c.IsDestroyed=true;EnemyEffects.Forget(c);Check(EnemyEffects.Count==0,"dead monster's lease is dropped safely");
        Check(EnemyEffects.Kind("Unit_HFM_GanShi_01A_C")=="undead"&&EnemyEffects.Kind("Unit_HFM_GuYao_01_realstand_C")=="undead","corpses and bone demons are undead (Smite)");
        Check(EnemyEffects.Kind("Unit_PSD_ZhiZhu_01_C")=="arthropod","spiders are arthropods (Bane)");
        Check(EnemyEffects.Kind("Unit_HFM_Shu_05a_C")==""&&EnemyEffects.Kind(null)=="","rat soldiers get no category");
        Console.WriteLine("ENEMY EFFECTS PASS checks="+checks);return 0;
    }
}
namespace DqjCombat{internal static class McPlayerHealthGate{internal static readonly Dictionary<string,float> WeakScale=new Dictionary<string,float>();}}
namespace UnrealEngine.Runtime{public class UObject{public IntPtr Address=new IntPtr(1);public bool IsDestroyed,IsPendingKill;}}
namespace UnrealEngine.Engine{public class UWorld:UnrealEngine.Runtime.UObject{}public class AActor:UnrealEngine.Runtime.UObject{public UWorld World;public float CustomTimeDilation;}}
