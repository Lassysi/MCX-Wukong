using System;

namespace DqjCombat
{
    internal static class PlayerFormPolicy
    {
        // Live99: Unit_Player_Chan is a real controlled pawn, but its stealth/flight state is not MC locomotion.
        // Opt in the captured ordinary player, rather than assuming every BGUPlayerCharacter is human-shaped.
        internal static bool CanOwnMovement(string actorClass)
        {return string.Equals(actorClass,"Unit_Player_Wukong_C",StringComparison.OrdinalIgnoreCase);}
        internal static bool Enlarge(bool movie,bool interaction)
        {return movie&&!interaction;}
    }
}
