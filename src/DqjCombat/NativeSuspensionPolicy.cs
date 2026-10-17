namespace DqjCombat
{
    internal static class NativeSuspensionPolicy
    {
        // A camera movie / interaction is live native work, not an expired teleport acknowledgement.
        internal static bool IgnoreStuckTeleport(bool stuck,bool movie,bool interacting)
        {return stuck&&!movie&&!interacting;}
        internal static bool CanExpire(double elapsed,bool movie,bool interacting)
        {return elapsed>45&&!movie&&!interacting;}
    }
}
