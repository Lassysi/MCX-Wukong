using System;
using System.Collections.Generic;
using System.Linq;
using DqjCombat;
public sealed class RuntimeOldActor
{
    public void GetAttachedActors(out List<object> actors,bool reset){if(!reset)throw new Exception("reset");actors=new List<object>{"staff"};}
    public string GetAttachParentSocketName(){return "weapon_r";}
}
public sealed class RuntimeNewActor
{
    public void GetAttachedActors(out List<object> actors,bool reset,bool recursive){if(!reset||recursive)throw new Exception("argument contract");actors=new List<object>{"armor","gourd"};}
}
public sealed class BrokenActor
{
    public void GetAttachedActors(out List<object> actors,bool reset){throw new InvalidOperationException("native failure");}
}
internal static class Program
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Main()
    {
        var old=new RuntimeOldActor();
        Check(NativeAppearanceApi.AttachedActors(old).Cast<object>().Single().Equals("staff"),"two-argument installed runtime shape is supported");
        Check(NativeAppearanceApi.AttachedActors(new RuntimeNewActor()).Cast<object>().Count()==2,"three-argument SDK shape is supported without recursive ownership");
        Check(NativeAppearanceApi.Socket(old,"GetAttachParentSocketName")=="weapon_r","native weapon socket ownership is retained");
        Check(NativeAppearanceApi.Socket(old,"GetAttachSocketName")=="","optional missing socket method does not abort skin construction");
        bool unwrapped=false;try{NativeAppearanceApi.AttachedActors(new BrokenActor());}catch(InvalidOperationException e){unwrapped=e.Message=="native failure";}
        Check(unwrapped,"original native failure is exposed instead of opaque reflection exception");
        bool missing=false;try{NativeAppearanceApi.AttachedActors(new object());}catch(MissingMethodException){missing=true;}
        Check(missing,"unsupported runtime produces a precise compatibility diagnostic");
        var lease=new NativeMovementLease();int mode=1,custom=3,disabled=0,restored=0;
        Action disable=()=>{disabled++;mode=0;};Action<int,byte> restore=(m,c)=>{restored++;mode=m;custom=c;};
        lease.Release(restore);Check(restored==0&&mode==1,"failed preparation without movement ownership cannot freeze or restore the host CMC");
        lease.Hold(mode,(byte)custom,disable);lease.Hold(mode,(byte)custom,disable);
        Check(disabled==2&&mode==0,"committed ownership disables movement while preserving the initial mode");
        lease.Release(restore);lease.Release(restore);
        Check(restored==1&&mode==1&&custom==3,"release restores the original custom/native mode exactly once");
        mode=3;custom=0;lease.Hold(mode,(byte)custom,disable);lease.Release(restore);
        Check(restored==2&&mode==3,"a new post-animation lease captures the new movement mode");
        Console.WriteLine("APPEARANCE API AND MOVEMENT LEASE PASS checks="+checks);
    }
}
