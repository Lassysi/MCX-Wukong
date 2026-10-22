using System;

namespace DqjCombat
{
    // MC owns duration/milk/death removal. Native immunity is a short renewable lease on this world link.
    internal sealed class NativeAppleProtection : IDisposable
    {
        private readonly object collection;
        private readonly Func<double> clock;
        private EventHandle handle;
        private Delegate original,mine;
        private double until=-100;
        private bool failed;
        internal NativeAppleProtection(object collection,Func<double> clock){this.collection=collection;this.clock=clock;}
        internal bool Active{get{return clock()<until;}}
        internal static bool Blocks(object[] args,bool active)
        {
            if(!active||args==null||args.Length!=5||args[0]==null||!args[0].GetType().IsEnum||!(args[3] is float))return false;
            int kind=Convert.ToInt32(args[0]);float amount=(float)args[3];
            return kind>=1&&kind<=6&&amount>0&&!float.IsNaN(amount)&&!float.IsInfinity(amount);
        }
        internal void Receive(int remainingTicks)
        {
            bool was=Active;until=remainingTicks>0?clock()+Math.Min(3,Math.Min(remainingTicks,2400)/20.0):-100;
            if(!Active){Restore();return;}
            if(failed)return;
            try{
                if(handle==null)Install();
                if(!was){
                    // Native abnormal handlers clear buildup, final effects and their display through their own lifecycle.
                    NativeEventInvoker.Invoke(NativeEventInvoker.Signal(collection,"Evt_ClearAllAbnormalState"));
                    Log.Write("apple","native abnormal states cleared; timed immunity enabled");
                }
            }catch(Exception e){failed=true;until=-100;Restore();Log.Write("apple-error","native status protection unavailable: "+e.Message);}
        }
        private void Install()
        {
            var candidate=HookLedger.Resolve(collection,"Evt_HandleAbnormal");var handlers=HookLedger.ReadHandlers(candidate);
            Delegate selected=null;
            if(handlers!=null)foreach(var handler in handlers)
                if(handler.Target!=null&&handler.Target.GetType().FullName=="b1.BUS_AbnormalStateCompImpl"&&handler.Method.Name=="HandleAbnormal"){
                    if(selected!=null)throw new InvalidOperationException("Multiple native abnormal owners");selected=handler;
                }
            if(selected==null)throw new InvalidOperationException("Native abnormal owner not ready");
            NativeEventInvoker.Validate(NativeEventInvoker.Signal(collection,"Evt_ClearAllAbnormalState"));
            var invoke=selected.GetType().GetMethod("Invoke");var parameters=invoke.GetParameters();
            if(parameters.Length!=5||!parameters[0].ParameterType.IsEnum||parameters[3].ParameterType!=typeof(float))throw new InvalidOperationException("Native abnormal event signature changed");
            var native=selected;var replacement=DelegateFactory.Create(native.GetType(),args=>{
                if(!Blocks(args,Active))native.DynamicInvoke(args);
            });
            string error;
            if(!HookLedger.Uninstall(candidate,native,out error))throw new InvalidOperationException(error);
            if(!HookLedger.Install(candidate,replacement,out error)){
                string ignored;HookLedger.Install(candidate,native,out ignored);throw new InvalidOperationException(error);
            }
            handle=candidate;original=native;mine=replacement;
        }
        internal void Tick(){if(!Active&&handle!=null)Restore();}
        private void Restore()
        {
            if(handle==null)return;string error;HookLedger.Uninstall(handle,mine,out error);
            bool present=false;var current=HookLedger.ReadHandlers(handle);
            if(current!=null)foreach(var handler in current)if(handler.Equals(original))present=true;
            if(!present)HookLedger.Install(handle,original,out error);
            handle=null;original=null;mine=null;
        }
        public void Dispose(){until=-100;Restore();}
    }
}
