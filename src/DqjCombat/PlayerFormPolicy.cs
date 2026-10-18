using System;

namespace DqjCombat
{
    internal static class PlayerFormPolicy
    {
        // Live99: Unit_Player_Chan is a real controlled pawn, but its stealth/flight state is not MC locomotion.
        // Opt in the captured ordinary player, rather than assuming every BGUPlayerCharacter is human-shaped.
        // Preview102 (user: later forms such as the Erlang phase-2 giant monkey are still Steve, so MC logic should
        // drive them): any controlled player form whose skeleton has the humanoid joints is MC-driven. Only forms that
        // are not walking bodies (the cicada's stealth/flight) keep native controls.
        internal static bool CanOwnMovement(string actorClass,bool humanoidRig)
        {
            if(string.Equals(actorClass,"Unit_Player_Wukong_C",StringComparison.OrdinalIgnoreCase))return true;
            if(NativeOnlyForm(actorClass))return false;
            return humanoidRig;
        }
        internal static bool NativeOnlyForm(string actorClass)
        {return actorClass!=null&&actorClass.StartsWith("Unit_Player_Chan",StringComparison.OrdinalIgnoreCase);}
        internal static bool Enlarge(bool movie,bool interaction)
        {return movie&&!interaction;}
    }
}
