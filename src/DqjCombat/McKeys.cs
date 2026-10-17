using System.Collections.Generic;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.InputCore;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    internal static class McKeys
    {
        private static readonly Dictionary<string, FKey> keys = new Dictionary<string, FKey>();
        internal static FKey Key(string name)
        {
            FKey key;
            if (!keys.TryGetValue(name, out key))
            {
                key = UGSE_UMGFuncLib.GetKeyWithName(new FName(name));
                keys.Add(name, key);
            }
            return key;
        }
        internal static bool Down(APlayerController controller, string name)
        {
            return controller.IsInputKeyDown(Key(name));
        }
    }
}
