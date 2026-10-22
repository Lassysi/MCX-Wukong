using System;
using DqjCombat;

namespace BtlShare{public sealed class Stub{}}
namespace UnrealEngine.Engine{public sealed class Stub{}}
namespace UnrealEngine.Runtime{public sealed class Stub{}}
namespace b1.EventDelDefine{public sealed class Stub{}}
namespace DqjCombat{internal static class Log{internal static void Write(string category,string message){Console.WriteLine(category+": "+message);}}}
namespace b1{
    internal enum Status{None,Freeze,Burn,Poison,Thunder,Yin,Yang,Unknown}
    internal delegate void Abnormal(Status kind,object attacker,int mode,float increase,int effect);
    internal sealed class AbnormalSignal{
        internal Abnormal _InvokeCallBack;
        public void Invoke(Status kind,object attacker,int mode,float increase,int effect){_InvokeCallBack?.Invoke(kind,attacker,mode,increase,effect);}
    }
    internal sealed class VoidSignal{
        internal Action _InvokeCallBack;
        public void Invoke(){_InvokeCallBack?.Invoke();}
    }
    internal sealed class Collection{
        public AbnormalSignal Evt_HandleAbnormal{get;}=new AbnormalSignal();
        public VoidSignal Evt_ClearAllAbnormalState{get;}=new VoidSignal();
    }
    internal sealed class BUS_AbnormalStateCompImpl{
        internal int hits,clears;
        internal void HandleAbnormal(Status kind,object attacker,int mode,float increase,int effect){hits++;}
        internal void Clear(){clears++;}
    }
}
internal static class Program{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static int Main(){
        double now=0;var collection=new b1.Collection();var native=new b1.BUS_AbnormalStateCompImpl();int observer=0,later=0;
        collection.Evt_HandleAbnormal._InvokeCallBack+=native.HandleAbnormal;
        collection.Evt_HandleAbnormal._InvokeCallBack+=(a,b,c,d,e)=>observer++;
        collection.Evt_ClearAllAbnormalState._InvokeCallBack+=native.Clear;
        var guard=new NativeAppleProtection(collection,()=>now);
        guard.Receive(2400);Check(guard.Active&&native.clears==1,"activation must clear existing native abnormalities once");
        foreach(b1.Status status in new[]{b1.Status.Freeze,b1.Status.Burn,b1.Status.Poison,b1.Status.Thunder,b1.Status.Yin,b1.Status.Yang})collection.Evt_HandleAbnormal.Invoke(status,null,0,10,0);
        Check(native.hits==0&&observer==6,"all six native abnormal consumers blocked; unrelated observers kept");
        collection.Evt_HandleAbnormal.Invoke(b1.Status.None,null,0,10,0);collection.Evt_HandleAbnormal.Invoke(b1.Status.Unknown,null,0,10,0);
        collection.Evt_HandleAbnormal.Invoke(b1.Status.Poison,null,0,-10,0);collection.Evt_HandleAbnormal.Invoke(b1.Status.Poison,null,0,float.NaN,0);
        Check(native.hits==4,"unknown/recovery/nonfinite events follow native consumer");
        now=2;guard.Receive(2400);Check(native.clears==1,"heartbeat must not repeatedly clear unrelated state");
        collection.Evt_HandleAbnormal._InvokeCallBack+=(a,b,c,d,e)=>later++;
        now=5.1;guard.Tick();Check(!guard.Active,"lost heartbeat expires after three seconds");
        collection.Evt_HandleAbnormal.Invoke(b1.Status.Poison,null,0,10,0);Check(native.hits==5&&later==1,"expiry restores original without losing later subscribers");
        guard.Receive(1);now+=.06;guard.Tick();Check(!guard.Active,"short remaining duration respected");
        guard.Receive(2400);guard.Receive(0);collection.Evt_HandleAbnormal.Invoke(b1.Status.Burn,null,0,10,0);Check(native.hits==6,"milk explicitly restores native behavior");
        guard.Receive(2400);guard.Dispose();collection.Evt_HandleAbnormal.Invoke(b1.Status.Freeze,null,0,10,0);Check(native.hits==7,"world disposal restores handler");
        Check(HookLedger.ReadHandlers(HookLedger.Resolve(collection,"Evt_HandleAbnormal")).Length==3,"no duplicate native handler after repeated activation and expiry");
        var empty=new b1.Collection();var unsupported=new NativeAppleProtection(empty,()=>now);unsupported.Receive(2400);Check(!unsupported.Active,"unknown native owner fails closed without an immune lease");
        Console.WriteLine("PASS native apple protection: six abnormalities, exact delegate contract, native clear lifecycle, observer preservation, heartbeat expiry, milk/dispose and unknown-owner refusal");return 0;
    }
}
