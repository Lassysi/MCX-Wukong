using System;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    internal static class NativeObjectLifetime
    {
        // IsDestroyed only checks objRef. Actor.World calls native GetWorld(Address) without
        // CheckDestroyed; the captured chapter crash passed Address=0 to that native export.
        internal static bool Live(UObject value)
        {
            return !ReferenceEquals(value,null)&&!value.IsDestroyed&&value.Address!=IntPtr.Zero&&!value.IsPendingKill;
        }
        internal static UWorld ActorWorld(AActor actor)
        {
            if(!Live(actor))return null;
            var world=actor.World;
            return Live(world)?world:null;
        }
    }
}
