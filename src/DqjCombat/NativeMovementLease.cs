using System;
namespace DqjCombat
{
    // Ground preparation does not acquire this lease. Only committed MC movement may disable the host CMC.
    internal sealed class NativeMovementLease
    {
        private bool held;
        private int mode;
        private byte custom;
        internal void Hold(int current,byte currentCustom,Action disable)
        {if(!held){mode=current;custom=currentCustom;held=true;}disable();}
        internal void Release(Action<int,byte> restore)
        {if(!held)return;restore(mode,custom);held=false;}
    }
}
