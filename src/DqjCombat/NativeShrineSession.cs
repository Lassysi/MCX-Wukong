using System;

namespace DqjCombat
{
    internal sealed class NativeShrineState
    {
        internal int Id;
        internal bool SelectedShrine,SelectedInteractable,Busy,Activated;
        internal string Diagnostic;
        internal string TargetClass; // Preview102: native selected target class, for the MC interaction hint
    }
    // Native selection owns the target: shrines, gathering, chests and story interactions.
    // The original action owns rewards/quest flags. Only shrine completion counts as rest.
    internal sealed class NativeShrineSession
    {
        private readonly Func<int,NativeShrineState> read;
        private readonly Func<bool> leaveShrineMenu;
        private readonly Action openMap;
        private int id;
        private double started;
        private bool sawBusy,sawActivation,leftMenu,warned,isShrine;
        internal bool Active {get;private set;}
        /** Set when a native shrine interaction (rest/activation) completed; the host consumes it. */
        internal bool Rested {get;set;}
        internal NativeShrineSession(Func<int,NativeShrineState> read,Func<bool> leaveShrineMenu,Action openMap)
        {this.read=read;this.leaveShrineMenu=leaveShrineMenu;this.openMap=openMap;}
        internal bool Start(double now,Action suspend,Action nativePress)
        {
            if(Active)return true;
            var state=read(0);
            if(state.Busy||!(state.SelectedInteractable||state.SelectedShrine&&state.Id>0)){Log.Write("interaction","native interaction ignored: no idle selected target; "+state.Diagnostic);return false;}
            isShrine=state.SelectedShrine&&state.Id>0;
            Log.Write("interaction","native selected target; "+state.Diagnostic+" shrine="+isShrine+" activated="+state.Activated);
            // Preview54: an activated shrine also runs the original interaction. In Wukong
            // that is the rest itself (HP restore, enemies respawn) followed by the native
            // shrine menu, whose own travel entry opens the map. Previously this jumped
            // straight to the map, so resting (and fresh enemies for combat tests) was impossible.
            if(state.Activated)Log.Write("shrine","selected active shrine="+state.Id+"; native rest and shrine menu");
            id=isShrine?state.Id:0;started=now;sawBusy=false;sawActivation=false;leftMenu=false;warned=false;
            suspend();Active=true;
            try{nativePress();Log.Write("interaction","original native input dispatched; shrine="+id);}
            catch{Clear();throw;}
            return true;
        }
        internal bool Update(double now,bool nativeReady)
        {
            if(!Active)return false;
            var state=read(id);
            if(state.Busy&&!sawBusy){sawBusy=true;Log.Write("shrine","native interaction started; shrine="+id);}
            if(isShrine&&state.Activated&&!sawActivation){sawActivation=true;Log.Write("shrine","native controller confirms activated shrine="+id);}
            if(state.Busy||!nativeReady){
                if(!warned&&now-started>30){warned=true;Log.Write("shrine","native interaction still owns control; use native Return if a menu remains open; shrine="+id);}
                return true;
            }
            if(!sawBusy&&!sawActivation&&now-started<.75)return true;
            bool activated=sawActivation,wasBusy=sawBusy,rest=isShrine;int completedId=id;Clear();
            if(rest&&(activated||wasBusy))Rested=true;
            // The player chooses rest/travel/return in the native menu; no automatic map.
            Log.Write("interaction",!rest?"native selected interaction finished; busyObserved="+wasBusy:activated?"native shrine interaction finished; shrine="+completedId
                :wasBusy?"native shrine interaction canceled; resume MC when ready":"native shrine interaction did not start; resume MC when ready");
            return false;
        }
        internal void Clear(){Active=false;id=0;sawBusy=false;sawActivation=false;leftMenu=false;warned=false;isShrine=false;}
    }
}
