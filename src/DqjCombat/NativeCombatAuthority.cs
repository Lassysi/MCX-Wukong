namespace DqjCombat
{
    // Native combat sync may ignore native movement input. It does not own the MC session,
    // camera or HUD. Menus, movies, loading, death and a lost session revoke this lease.
    internal sealed class NativeCombatAuthority
    {
        private bool syncingInput;
        internal bool Gameplay(bool nativeGameplay,bool syncing,bool owns,bool combatContextReady)
        {
            if(!owns||!combatContextReady){syncingInput=false;return nativeGameplay;}
            if(syncing)syncingInput=true;
            else if(nativeGameplay)syncingInput=false;
            return nativeGameplay||syncingInput;
        }
        internal void Clear(){syncingInput=false;}
    }
}
