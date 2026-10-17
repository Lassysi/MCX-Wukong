using System;
using System.Collections.Generic;
using System.Reflection;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    internal sealed class NativeCameraEffects : IDisposable
    {
        private readonly List<UCameraModifier> owned = new List<UCameraModifier>();
        private APlayerCameraManager manager;
        private PropertyInfo ownerProperty;
        private double nextScan, nextError;

        internal void Update(APlayerController controller, double now)
        {
            try
            {
                var current = controller.PlayerCameraManager;
                if (current == null || current.IsDestroyed || current.IsPendingKill) { Dispose(); return; }
                if (current != manager) { Dispose(); manager = current; nextScan = 0; }
                if (ownerProperty == null)
                    ownerProperty = typeof(UCameraModifier).GetProperty("CameraOwner",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (now >= nextScan && ownerProperty != null)
                {
                    nextScan = now + 1;
                    foreach (UCameraModifier modifier in UObject.GetObjects<UCameraModifier>())
                    {
                        if (modifier == null || modifier.IsDestroyed || modifier.IsPendingKill || owned.Contains(modifier)) continue;
                        if ((ownerProperty.GetValue(modifier, null) as APlayerCameraManager) != manager || modifier.IsDisabled()) continue;
                        owned.Add(modifier);
                        Log.Write("camera-fx", "接管 modifier=" + modifier.GetClass().GetPathName());
                    }
                }
                for (int i = owned.Count - 1; i >= 0; i--)
                {
                    var modifier = owned[i];
                    if (modifier.IsDestroyed || modifier.IsPendingKill) { owned.RemoveAt(i); continue; }
                    if (!modifier.IsDisabled()) modifier.DisableModifier(true);
                }
                manager.StopAllCameraShakes(true);
            }
            catch (Exception e)
            {
                if (now >= nextError) { nextError = now + 5; Log.Write("camera-fx-error", e.Message); }
            }
        }

        public void Dispose()
        {
            foreach (var modifier in owned)
            {
                try { if (!modifier.IsDestroyed && !modifier.IsPendingKill) modifier.EnableModifier(); } catch { }
            }
            owned.Clear();
            manager = null;
        }
    }
}
