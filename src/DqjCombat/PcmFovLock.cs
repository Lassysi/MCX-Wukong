using System;
using System.Globalization;
using UnrealEngine.Engine;
using UnrealEngine.Plugins.TemplateSequence;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // Preview40. Live39 proved the native CameraActor is the view target of
    // BP_B1PlayerCameraManager while GetFOVAngle stays 90, and the managed
    // UpdateCameraCS only forwards to native code. APlayerCameraManager's
    // GetFOVAngle returns LockedFOV when set, and the local player view uses it.
    // The standard "fov" exec sets LockedFOV; "fov 0" clears it on release.
    internal sealed class PcmFovLock
    {
        private static readonly float Tolerance=.05f;
        private static readonly double RetrySeconds=2;
        private float locked;
        private double nextAttempt;
        private int attempts;
        private bool cacheUnavailable;

        internal bool Active{get{return locked>0;}}

        internal void Apply(UObject context,APlayerController controller,APlayerCameraManager manager,float desired,double now)
        {
            // Once locked, Follow keeps the lock on the rendered MC frame's FOV.
            if(locked>0)return;
            if(Math.Abs(manager.GetFOVAngle()-desired)<=Tolerance)return;
            if(now<nextAttempt)return;
            // A lock that the manager reports but does not honor is evidence, not a retry loop.
            if(attempts>=3&&Math.Abs(locked-desired)<=.01f)return;
            nextAttempt=now+RetrySeconds;attempts++;
            float before=manager.GetFOVAngle();
            Execute(context,controller,"fov "+F(desired));
            locked=desired;
            Log.Write("camera-fov-lock","requested lock="+F(desired)+" attempt="+attempts+" pcmBefore="+F(before)+" pcmAfter="+F(manager.GetFOVAngle())+" "+Cache(controller));
        }

        // Preview45: called at begin-frame with the FOV of the exact MC frame the
        // compositor will show. Live44: a 2 s retry let MC's sprint FOV (112.7 deg)
        // run ahead of the host lock (102.4 deg), shrinking the composited blocks.
        internal void Follow(UObject context,APlayerController controller,float desired,double now)
        {
            if(locked<=0||Math.Abs(locked-desired)<=.01f)return;
            Execute(context,controller,"fov "+F(desired));
            locked=desired;follows++;
            if(now>=nextFollowLog){nextFollowLog=now+2;Log.Write("camera-fov-lock","follow lock="+F(desired)+" changes="+follows);follows=0;}
        }
        private int follows;
        private double nextFollowLog;

        internal void Release(UObject context,APlayerController controller)
        {
            if(locked<=0){attempts=0;return;}
            try{
                Execute(context,controller,"fov 0");
                var manager=controller.PlayerCameraManager;
                Log.Write("camera-fov-lock","released lock="+F(locked)+" pcmAfter="+(manager==null||manager.IsDestroyed?"n/a":F(manager.GetFOVAngle())));
            }catch(Exception e){Log.Write("camera-fov-lock-error","release: "+e.GetType().Name+": "+e.Message);}
            locked=0;attempts=0;nextAttempt=0;
        }

        // Camera cache POV is the manager's computed view before LockedFOV.
        internal string Cache(APlayerController controller)
        {
            if(cacheUnavailable)return "cache=unavailable";
            try{
                var pov=USequenceCameraShakeTestUtil.GetCameraCachePOV(controller);
                var last=USequenceCameraShakeTestUtil.GetLastFrameCameraCachePOV(controller);
                return "cacheH="+F(pov.FOV)+" cacheAspect="+F(pov.AspectRatio)+" cacheConstrain="+pov.ConstrainAspectRatio+" lastCacheH="+F(last.FOV);
            }catch(Exception e){cacheUnavailable=true;return "cache=unavailable("+e.GetType().Name+": "+e.Message+")";}
        }

        private static void Execute(UObject context,APlayerController controller,string command)
        {USystemLibrary.ExecuteConsoleCommand(context,command,controller);}
        private static string F(double value){return value.ToString("0.######",CultureInfo.InvariantCulture);}
    }
}
