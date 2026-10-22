// L0 Host 适配层 + 日志
// 规则：整个项目只有这一层允许出现反射、委托与引擎类型。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using BtlShare;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;
using b1;

namespace DqjCombat
{
    internal static class Log
    {
        private static string path;

        // 教训：加载器在 Develop 模式下从内存装配程序集，Assembly.Location 可能为空，
        // 静态初始化器里绝不能做可能抛异常的事。
        internal static string Path
        {
            get
            {
                if (path != null) return path;
                string dir = null;
                try
                {
                    string loc = typeof(Log).Assembly.Location;
                    if (!string.IsNullOrEmpty(loc)) dir = System.IO.Path.GetDirectoryName(loc);
                }
                catch { }
                if (string.IsNullOrEmpty(dir))
                {
                    try
                    {
                        string exe = Process.GetCurrentProcess().MainModule.FileName;
                        string gameDir = System.IO.Path.GetDirectoryName(exe);
                        string candidate = System.IO.Path.Combine(gameDir, "CSharpLoader", "Mods", "DqjCombat");
                        dir = Directory.Exists(candidate) ? candidate : gameDir;
                    }
                    catch { }
                }
                if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;
                path = System.IO.Path.Combine(dir, "dqj-combat.log");
                return path;
            }
        }

        /// <summary>模组所在目录（用于定位随包资源，如 steve.mesh）。</summary>
        internal static string Dir
        {
            get { return System.IO.Path.GetDirectoryName(Path); }
        }

        // Preview79 (user: clean up diagnostic logs, less stutter). Measurement-only categories
        // and per-frame probes run only when "dqj-debug.on" sits next to the log (checked once).
        private static int verbose = -1;
        internal static bool Verbose
        {
            get
            {
                if (verbose < 0)
                {
                    verbose = 0;
                    try { if (File.Exists(System.IO.Path.Combine(Dir, "dqj-debug.on"))) verbose = 1; } catch { }
                }
                return verbose == 1;
            }
        }
        private static readonly HashSet<string> VerboseOnly = new HashSet<string>{
            "camera-manager","camera-manager-type","camera-order","camera-projection","camera-fov-lock",
            "bridge-motion","bridge-ray","route-value","hit-probe","world-terrain","gameplay","world-link","bridge-input"};

        internal static void Write(string category, string message)
        {
            if (VerboseOnly.Contains(category) && !Verbose) return;
            try
            {
                File.AppendAllText(Path,
                    DateTime.Now.ToString("HH:mm:ss.fff") + " | " + category + " | " + message + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch { }
        }

        internal static void Echo(string message)
        {
            try { Console.WriteLine("[DqjCombat] " + message); } catch { }
        }

        internal static void Reset()
        {
            try { File.Delete(Path); } catch { }
        }
    }

    internal static class GameApi
    {
        internal static UWorld World()
        {
            try { return GCHelper.FindRef(FGlobals.GWorld).Managed as UWorld; }
            catch { return null; }
        }

        internal static APlayerController Controller(UWorld world)
        {
            try { return world == null ? null : UGSE_EngineFuncLib.GetFirstLocalPlayerController(world); }
            catch { return null; }
        }

        internal static APawn Pawn(APlayerController controller)
        {
            try { return controller == null ? null : controller.GetControlledPawn(); }
            catch { return null; }
        }

        internal static long Frame()
        {
            try { return UGSE_EngineFuncLib.GetGFrameCounter(); }
            catch { return 0; }
        }

        internal static bool Paused(UWorld world)
        {
            try { return world != null && UGameplayStatics.IsGamePaused(world); }
            catch { return false; }
        }

        internal static bool Loading()
        {
            try { return UObject.IsLoading(); }
            catch { return false; }
        }

        // Live65: a native teleport (map/shrine travel, rebirth) runs as a game-instance FSM;
        // BIC_TeleportData.CurrentInstance is set from InitAndStartTeleport until OnTeleportGraphFinish.
        // Preview67: native cutscene (movie/sequence) state; skipping uses the game's own skip.
        internal static bool MoviePlaying(UObject context, out bool canSkip)
        {
            canSkip = false;
            try
            {
                var data = BGU_DataUtil.GetGameStateReadonlyData<IBGC_MovieData, BGC_MovieData>(context);
                if (data == null || !data.IsPlaying()) return false;
                canSkip = data.IsCanSkip(); return true;
            }
            catch { return false; }
        }
        // Preview69: native interactions (squeeze-through gaps, climbs, shrines) move the
        // character with their own animation; MC must not hold the actor meanwhile.
        internal static bool PlayerInteracting(BGUPlayerCharacterCS player)
        {
            try
            {
                // BPS_PlayerInteractComp keeps this store on LocalPlayerState; the controller's store is empty.
                var data = NativeTravelCatalogue.ActorData(player,typeof(b1.UI.DS_IBPC_PlayerInteractData),"LocalPlayerState") as b1.UI.DS_IBPC_PlayerInteractData;
                return data != null && data.IsInInteraction();
            }
            catch { return false; }
        }
        // Items 1/4: unskippable cutscenes are made skippable (native flag) before the native skip.
        internal static void ForceSkippable(UObject context)
        {
            var data = BGU_DataUtil.GetGameStateReadonlyData<IBGC_MovieData, BGC_MovieData>(context) as BGC_MovieData;
            if (data == null) return;
            if (data.bAllSeqCantSkip) data.bAllSeqCantSkip = false;
            var movie = data.CameraMovieInstance;
            if (movie != null) movie.MarkCanBeSkipped(true);
        }
        internal static bool PlayerAnimationSyncing(UObject context)
        {
            try
            {
                var data = BGU_DataUtil.GetGameStateReadonlyData<IBGC_AnimationSyncData, BGC_AnimationSyncData>(context);
                return data != null && data.IsPlayerInAnimationSyncing(context);
            }
            catch { return false; }
        }
        internal static bool NativeTeleporting(UObject context)
        {
            try
            {
                var manager = BGWGameInstanceCS.GetObject<BGW_GameDataMgr>(context);
                var data = manager == null ? null : manager.GetGameInstanceWritableData<BIC_TeleportData>();
                return data != null && data.CurrentInstance != null;
            }
            catch { return false; }
        }

        internal static bool InGameplay(AActor pawn, APlayerController controller, UWorld world)
        {return InGameplayCore(pawn,controller,world,false);}
        internal static bool InGameplayWithBridgeCursor(AActor pawn,APlayerController controller,UWorld world)
        {return InGameplayCore(pawn,controller,world,true);}
        internal static bool InGameplayDuringCombatSync(AActor pawn,APlayerController controller,UWorld world,bool bridgeCursor)
        {return InGameplayCore(pawn,controller,world,bridgeCursor,true);}
        internal static string GameplayGates(APlayerController controller,UWorld world)
        {
            try{
                var lifetime=BGW_GameLifeTimeMgr.Get(world);
                return "paused="+Paused(world)+" moveIgnored="+controller.IsMoveInputIgnored()+" cursor="+controller.ShowMouseCursor
                    +" battle="+(lifetime!=null&&lifetime.IsInFSMState(SGI_Global.InBattleStandAlone));
            }catch(Exception e){return "gate-error="+e.GetType().Name;}
        }
        private static bool InGameplayCore(AActor pawn,APlayerController controller,UWorld world,bool bridgeCursor,bool combatSync=false)
        {
            try
            {
                var player = pawn as BGUPlayerCharacterCS;
                if (player == null || player.IsDestroyed || player.IsPendingKill || Paused(world)
                    || (!combatSync&&controller.IsMoveInputIgnored()) || (controller.ShowMouseCursor && !bridgeCursor)
                    || BGUFunctionLibraryCS.BGUIsUnitDead(player)) return false;
                // 仅有 Pawn 并不代表加载结束。旧日志在 Loading_TravelLevel 就挂上了 HUD。
                var lifetime = BGW_GameLifeTimeMgr.Get(world);
                return lifetime != null && lifetime.IsInFSMState(SGI_Global.InBattleStandAlone);
            }
            catch { return false; }
        }

        internal static BUS_GSEventCollection Events(AActor actor)
        {
            try { return BUS_EventCollectionCS.Get(actor); }
            catch { return null; }
        }
    }
}
