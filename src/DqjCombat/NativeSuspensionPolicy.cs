namespace DqjCombat
{
    internal static class NativeSuspensionPolicy
    {
        // A camera movie / interaction is live native work, not an expired teleport acknowledgement.
        internal static bool IgnoreStuckTeleport(bool stuck,bool movie,bool interacting)
        {return stuck&&!movie&&!interacting;}
        internal static bool CanExpire(double elapsed,bool movie,bool interacting)
        {return elapsed>45&&!movie&&!interacting;}
        // Preview125: native slide / spline-move volumes (Live124 SplineFlyMove_C_1 = BP_SplineFlyMoveBase_C) hand control to Wukong.
        internal static bool IsSlideVolume(string className)
        {
            return className!=null&&(className.IndexOf("SplineFlyMove",System.StringComparison.OrdinalIgnoreCase)>=0||className.IndexOf("SplineMove",System.StringComparison.OrdinalIgnoreCase)>=0);
        }
    }
}
