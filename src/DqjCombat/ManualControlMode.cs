using System.Threading;

namespace DqjCombat
{
    internal enum ManualControlChange { None,EnterNative,ResumeMinecraft,Waiting,RejectedDead }
    // Owned by the mod session, not a pawn: chapter/teleport replacement must retain user intent.
    internal sealed class ManualControlMode
    {
        private int requests;
        internal bool NativeRequested {get;private set;}
        internal bool NativeActive {get;private set;}
        internal void RequestToggle(){Interlocked.Increment(ref requests);}
        internal ManualControlChange Step(bool gameplayReady,bool mcAlive,bool connected)
        {
            int count=Interlocked.Exchange(ref requests,0);
            if((count&1)!=0)NativeRequested=!NativeRequested;
            if(!NativeActive&&NativeRequested){
                if(!mcAlive){NativeRequested=false;return ManualControlChange.RejectedDead;}
                if(!gameplayReady)return ManualControlChange.Waiting;
                NativeActive=true;return ManualControlChange.EnterNative;
            }
            if(NativeActive&&!NativeRequested){
                if(!gameplayReady||!mcAlive||!connected)return ManualControlChange.Waiting;
                NativeActive=false;return ManualControlChange.ResumeMinecraft;
            }
            return ManualControlChange.None;
        }
    }
}
