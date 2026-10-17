using System;
using DqjCombat;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

internal static class Program
{
    private static int checks;
    private static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    private static int Main()
    {
        Check(!NativeObjectLifetime.Live(null),"null wrapper");
        var actor=new AActor();
        Check(NativeObjectLifetime.ActorWorld(actor)==actor.Target&&actor.WorldReads==1,"live world lookup");
        actor.Address=IntPtr.Zero;
        Check(!actor.IsDestroyed,"reproduce objRef-present/zero-Address distinction");
        int pending=actor.PendingReads;
        Check(!NativeObjectLifetime.Live(actor)&&actor.PendingReads==pending,"zero Address must not enter native pending-kill call");
        Check(NativeObjectLifetime.ActorWorld(actor)==null&&actor.WorldReads==1,"crash signature must not enter native GetWorld");
        actor.Address=new IntPtr(1);actor.IsDestroyed=true;
        Check(NativeObjectLifetime.ActorWorld(actor)==null&&actor.WorldReads==1,"destroyed actor must not enter GetWorld");
        actor.IsDestroyed=false;actor.Pending=true;
        Check(NativeObjectLifetime.ActorWorld(actor)==null&&actor.WorldReads==1,"pending actor must not enter GetWorld");
        actor.Pending=false;actor.Target.Address=IntPtr.Zero;
        Check(NativeObjectLifetime.ActorWorld(actor)==null,"zero Address world is not a new session");
        actor.Target=new UWorld();
        Check(NativeObjectLifetime.ActorWorld(actor)==actor.Target,"next live world becomes available");
        Console.WriteLine("NATIVE LIFETIME LINKED-CODE PASS checks="+checks+"; GetWorld zero-address call rejected; stub only");
        return 0;
    }
}
namespace UnrealEngine.Runtime
{
    public class UObject
    {
        public IntPtr Address=new IntPtr(1);public bool IsDestroyed,Pending;public int PendingReads;
        public bool IsPendingKill {get{PendingReads++;if(Address==IntPtr.Zero)throw new Exception("native IsPendingKill(0)");return Pending;}}
    }
}
namespace UnrealEngine.Engine
{
    public class UWorld:UObject{}
    public class AActor:UObject
    {
        public int WorldReads;public UWorld Target=new UWorld();
        public UWorld World {get{WorldReads++;if(Address==IntPtr.Zero)throw new Exception("native GetWorld(0), captured AV read +8");return Target;}}
    }
}
