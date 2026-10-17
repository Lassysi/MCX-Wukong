// 入口：会话接入 + 基线核对 + 分层功能（相机 / HUD / 输入接管）
//
// 重要约定（见 模组开发雷区.md）：
//   - 本文件不得出现 const 字段（会让加载器的 Cecil 解析 mscorlib 而失败）
//   - 冷启动时禁止用 FThreading.RunOnGameThreadAsync 提交任务：
//     游戏线程尚未起来，提交的任务会在启动期引发原生访问违例。
//     必须由后台线程等到游戏真正运行后，再用同步方式登记 ticker。
//   - Tick 必须暖机；Pawn 刚出现时组件未挂全，不能立刻挂钩子
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using CSharpModBase;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;
using UnrealEngine.Runtime.Native;
using UnrealEngine.UMG;

namespace DqjCombat
{
    public sealed class Mod : ICSharpMod
    {
        public string Name { get { return "DqjCombat"; } }
        public string Version
        {
            get
            {
#if S13 && S15
                return "1.0.16-MC121-BridgePreview101";
#elif S13
                return "0.2.0-S13";
#elif S12
                return "0.2.0-S12";
#else
                return "0.2.0-S11";
#endif
            }
        }

        private static readonly int FirstDelayMs = 30000;
        private static readonly int RetryDelayMs = 20000;
        private static readonly int MaxAttempts = 3;
        private static readonly int WarmupFrames = 60;

        private volatile bool active;
        private volatile bool tickerReady;
        private volatile bool tickSeen;
        private volatile bool toggleCameraRequested;
        private int travelRequested;
        private Native_FTicker.Del_RegisterTicker tickCallback;
        private FDelegateHandle tickHandle;
        // Static: the runtime's multicast Bind rejects instance callbacks.
        private static Mod current;
        private static readonly FSimpleMulticastDelegate beginFrameCallback=new FSimpleMulticastDelegate(BeginFrameStatic);
        private static bool beginFrameBound;
        private static double nextBeginFrameError;
        private readonly Stopwatch clock = new Stopwatch();
        private int tickCount;
        private double nextGameplayLog;
        private bool lastGameplay;

        private AActor sessionPawn;
        private UWorld sessionWorld;
        private InputObserver observer;
        private bool baselineDone;
        private int attachAttempts;

        private McCameraRig camera;
        private bool cameraTried;
        private readonly NativeCameraEffects cameraEffects = new NativeCameraEffects();
        private McBridgeHost bridge;
        private bool bridgeMode;
        private readonly PrologueSkip prologueSkip = new PrologueSkip();
        private readonly StoryPresentation storyPresentation = new StoryPresentation();
#if S12
        private McHud hud;
        private readonly NativeBattleHud battleHud = new NativeBattleHud();
        private readonly NativeBossHud bossHud = new NativeBossHud();
        private bool widgetLogged;
        private AHUD nativeHud;
        private APlayerController nativeHudController;
        private bool nativeHudOriginalShow;
        private double nativeHudNextRefresh;
        private double nativeHudNextLog;
        private static readonly double NativeHudRefreshSeconds = 1.0;
        private static readonly double NativeHudLogSeconds = 60.0;
#endif
#if S13
        private McInputRouter router;
        private int routerAttempts;
#endif
#if S14
        private SteveBody body;
        private UMaterialInterface bodyMaterial;
        private bool bodyTried;
#endif
#if S15
        private McMovement movement;
#endif
#if S13 && S15
        private McCombat combat;
        private bool combatTried;
#endif

        // ------------------------------------------------------------ 生命周期

        public void Init()
        {
            try
            {
                Log.Reset();
                Log.Write("init", "B 日志已重置 " + Log.Path);
                Log.Write("init", "版本 " + Version);
                bridgeMode=System.IO.File.Exists(System.IO.Path.Combine(Log.Dir,"mc-bridge.enabled"));
                Log.Write("bridge","real MC mode="+bridgeMode);
                Log.Echo("Init, log = " + Log.Path);

                active = true;
                clock.Start();
                Log.Write("init", "C 状态已置位");

                tickCallback = delegate (float _)
                {
                    try { if (active) Tick(); }
                    catch (Exception e) { Log.Write("tick-error", e.GetType().Name + ": " + e.Message); }
                    return active;
                };
                Log.Write("init", "D tickCallback 已创建");

                var worker = new Thread(RegisterWhenReady);
                worker.IsBackground = true;
                worker.Name = "DqjRegister";
                worker.Start();
                Log.Write("init", "G 延迟登记线程已启动（首次等待 " + FirstDelayMs + " ms）");
            }
            catch (Exception e)
            {
                Log.Write("init-error", e.GetType().Name + ": " + e.Message);
            }
        }

        private void RegisterWhenReady()
        {
            int delay = FirstDelayMs;
            for (int attempt = 1; attempt <= MaxAttempts && !tickerReady; attempt++)
            {
                try { Thread.Sleep(delay); } catch { return; }

                Log.Write("reg", "第 " + attempt + " 次尝试登记（已等 " + delay + " ms）");
                try { FThreading.RunOnGameThread(RegisterTicker); }
                catch (Exception e) { Log.Write("reg-error", e.GetType().Name + ": " + e.Message); }

                try { Thread.Sleep(6000); } catch { return; }
                if (tickSeen) break;
                delay = RetryDelayMs;
            }
            Log.Write("reg", tickerReady ? ("登记完成 tickSeen=" + tickSeen) : "登记未完成");

            // F5 切视角：游戏已经起来之后再注册热键，避免启动早期风险
            try
            {
                Utils.RegisterKeyBind(CSharpModBase.Input.Key.F5,
                    delegate { toggleCameraRequested = true; });
                Log.Write("reg", "F5 已绑定（切换 MC 视角）");
            }
            catch (Exception e)
            {
                Log.Write("reg-error", "绑定 F5 失败 " + e.GetType().Name + ": " + e.Message);
            }
            if(bridgeMode)try{
                // Latch on the loader input event thread; engine work stays on Tick.
                Utils.RegisterKeyBind(CSharpModBase.Input.Key.F6,delegate{
                    if(active){Interlocked.Exchange(ref travelRequested,1);Log.Write("travel-key","F6 event queued");}
                });
                Log.Write("reg","F6 event binding registered (native travel catalogue)");
            }catch(Exception e){Log.Write("reg-error","F6 event binding: "+e.Message);}
        }

        private void RegisterTicker()
        {
            if (tickerReady) return;
            Native_FTicker.Reg_CoreTicker(IntPtr.Zero, tickCallback, ref tickHandle, true, 0);
            tickerReady = true;
            Log.Write("reg", "ticker 已登记（延迟登记路径）");
            if (bridgeMode)
            {
                try { current = this; FCoreDelegates.OnBeginFrame.Bind(beginFrameCallback); beginFrameBound = true; Log.Write("reg", "begin-frame camera callback bound"); }
                catch (Exception e) { Log.Write("reg-error", "begin-frame bind: " + e.GetType().Name + ": " + e.Message); }
            }
            Log.Echo("ticker registered (deferred)");
        }

        public void DeInit()
        {
            active = false;
            try
            {
                if (tickerReady)
                {
                    FThreading.RunOnGameThread(delegate
                    {
                        try { if (beginFrameBound) { FCoreDelegates.OnBeginFrame.Unbind(beginFrameCallback); beginFrameBound = false; } } catch { }
                        current = null;
                        Detach();
                        storyPresentation.Clear();
                        try
                        {
                            if (tickCallback != null)
                                Native_FTicker.Reg_CoreTicker(IntPtr.Zero, tickCallback, ref tickHandle, false, 0);
                        }
                        catch { }
                    });
                }
            }
            catch { }
            tickCallback = null;
            Log.Write("deinit", "已停止");
            Log.Echo("stopped");
        }

        // ------------------------------------------------------------ 每帧

        private double lastTickTime;

        private static void BeginFrameStatic()
        {
            var mod = current;
            if (mod == null || !mod.active || mod.bridge == null) return;
            try { mod.bridge.BeginFrame(); }
            catch (Exception e)
            {
                double now = mod.clock.Elapsed.TotalSeconds;
                if (now >= nextBeginFrameError) { nextBeginFrameError = now + 5; Log.Write("begin-frame-error", e.GetType().Name + ": " + e.Message); }
            }
        }

        private void Tick()
        {
            tickSeen = true;
            tickCount++;

            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, now - lastTickTime);
            lastTickTime = now;
            if (tickCount <= 4) Log.Write("tick", "第 " + tickCount + " 帧（暖机中）");
            if (tickCount < WarmupFrames) return;

            if (GameApi.Loading()) {if(bridge!=null)bridge.Invalidate();storyPresentation.Clear();return;}
            UWorld world = GameApi.World();
            if (!NativeObjectLifetime.Live(world)) {if(bridge!=null)bridge.Invalidate();return;}
            if(sessionWorld!=world){
                if(sessionWorld!=null){Log.Write("session","world changed; detach previous chapter before reading new actors");Detach();storyPresentation.Clear();}
                sessionWorld=world;
            }
            if (GameApi.Frame() < 120) return;

            if (bridgeMode && prologueSkip.Update(now,world,delegate { if(bridge!=null)bridge.PrepareForPrologueSkip(); })) return;

            if (bridgeMode && bridge == null) McBridgeHost.EarlyCutsceneSkip(now, world);
            APlayerController controller = GameApi.Controller(world);
            if (!NativeObjectLifetime.Live(controller)) {if(bridge!=null)bridge.Invalidate();return;}
            AActor pawn = GameApi.Pawn(controller);
            if (!NativeObjectLifetime.Live(pawn)) {
                if(bridgeMode){if(bridge!=null||sessionPawn!=null)Detach();storyPresentation.Update(now,world,controller,null,false);}
                return;
            }

            // ---- 会话：Pawn 变化就整体重挂 ----
            if (sessionPawn != null && sessionPawn != pawn)
            {
                Log.Write("session", "Pawn 变化，重挂");
                Detach();
            }
            if (sessionPawn == null)
            {
                sessionPawn = pawn;
                Log.Write("session", "接入 pawn=" + pawn.GetClass().GetPathName());
            }

            // DefaultEmptyPawn is a chapter/loading camera, not an actor with player ECS components.
            // Story presentation still runs independently through this possession change.
            bool ready = pawn is BGUPlayerCharacterCS&&IsComponentReady(pawn);
            bool inGameplay = GameApi.InGameplay(pawn, controller, world);
            if(bridge!=null&&bridge.ScreenOwnsCursor)inGameplay=GameApi.InGameplayWithBridgeCursor(pawn,controller,world);
            if (inGameplay != lastGameplay || now >= nextGameplayLog)
            {
                lastGameplay = inGameplay;
                nextGameplayLog = now + 10;
                Log.Write("gameplay", "active=" + inGameplay + " ready=" + ready
                    + " pawn=" + pawn.GetClass().GetName()+" "+GameApi.GameplayGates(controller,world));
            }
            if(bridgeMode)
            {
                if(bridge==null&&ready&&pawn is BGUPlayerCharacterCS){
                    storyPresentation.ReleaseOwner(pawn);
                    bridge=new McBridgeHost((BGUPlayerCharacterCS)pawn,controller);
                }
                storyPresentation.Update(now,world,controller,bridge==null?null:bridge.PlayerSkinOwner,
                    (bridge!=null&&bridge.NativeUiActive)||(pawn is BGUPlayerCharacterCS&&GameApi.PlayerInteracting((BGUPlayerCharacterCS)pawn)));
                if(Interlocked.Exchange(ref travelRequested,0)!=0){
                    if(bridge!=null&&inGameplay)bridge.RequestTravel();
                    else Log.Write("travel-key","F6 ignored outside ready gameplay");
                }
                if(bridge!=null)bridge.Update(now,inGameplay);
                bool owns=bridge!=null&&bridge.Active;
#if S12
                battleHud.Update(now,owns);
                bossHud.Update(now,owns,controller);
                if(owns)HideNativeHud(controller);else RestoreNativeHud();
#endif
                if(owns&&(bridge==null||!bridge.NativeUiActive))cameraEffects.Update(controller,now);else cameraEffects.Dispose();
                return;
            }
#if S13
            if (router != null)
            {
                router.GameplayActive = inGameplay;
                if (inGameplay) router.EnsureAttached();
            }
#endif

            // ---- F5 请求 ----
            if (toggleCameraRequested)
            {
                toggleCameraRequested = false;
                if (camera == null && ready && inGameplay) TryCreateCamera(pawn, controller);
                if (camera != null && inGameplay) camera.Cycle();
            }

            // ---- 相机（S1.1）----
            if (ready && inGameplay)
            {
                if (camera == null && !cameraTried) TryCreateCamera(pawn, controller);
                if (camera != null && !camera.Active)
                {
                    if (camera.Enable()) Log.Write("camera", "相机就绪 " + camera.Inspect());
                }
            }
            if (camera != null)
            {
                camera.SetGameplay(inGameplay);
                if (inGameplay) camera.Update();
            }
            if (inGameplay && camera != null && camera.Active) cameraEffects.Update(controller, now);
            else cameraEffects.Dispose();

#if S15
            // ---- MC 移动手感（S1.5a）----
            if (ready && inGameplay)
            {
                var pcMove = pawn as BGUPlayerCharacterCS;
                if (pcMove != null)
                {
                    if (movement == null)
                    {
                        movement = new McMovement(pcMove, controller, camera);
                        if (movement.Enable())
                        {
                            // 只有 CMC 可用时才接管移动，否则把移动透传给原生，避免把角色冻住
                            McInputRouter.TakeoverMovement = movement.Active;
                            Log.Write("move", "CMC 接管开关 TakeoverMovement=" + McInputRouter.TakeoverMovement);
                        }
                    }
                }
            }
            if (movement != null)
            {
#if S13
                movement.Update(inGameplay && router != null && router.Installed);
#else
                movement.Update(inGameplay);
#endif
            }
#endif

#if S13 && S15
            if (inGameplay && ready && !combatTried && camera != null && camera.Active
                && movement != null && movement.Active)
            {
                combatTried = true;
                combat = new McCombat((BGUPlayerCharacterCS)pawn, camera);
                combat.Enable();
            }
            if (combat != null) combat.Update(now, inGameplay);
            if (router != null) router.TakeoverCombat = combat != null && combat.Active;
#endif

#if S14
            // ---- Steve 身体（S1.4）----
            if (body != null) body.SetGameplay(inGameplay);
            if (ready && inGameplay)
            {
                if (body == null && !bodyTried) TryCreateBody(pawn);
                if (body != null)
                {
                    bool firstPerson = camera != null && camera.Active
                        && camera.Mode == McCameraRig.ViewMode.FirstPerson;
                    double swing = 0;
#if S13 && S15
                    if (combat != null) swing = combat.Swing;
#endif
                    body.Update(dt, firstPerson, camera, swing);
                }
            }
#endif

#if S12
            // ---- MC HUD（S1.2）----
            // 只有真正进入游戏（可操作角色）才显示；加载界面 / 菜单一律隐藏
            if (inGameplay)
            {
                if (hud == null) TryCreateHud(pawn, controller);
                if (hud != null && !hud.Active) hud.Enable();
            }
            if (hud != null)
            {
                hud.SetVisible(inGameplay);
                if (inGameplay) hud.Update();
            }
            // AHUD 开关不足以隐藏 GSUI；同时接管已确认的 BattleMainCon 根控件。
            battleHud.Update(now, inGameplay);
            if (inGameplay) HideNativeHud(controller);
            else RestoreNativeHud();
            if (inGameplay && !widgetLogged) { widgetLogged = true; LogViewportWidgets(world); }
#endif

            // ---- 输入观察 + 基线核对 ----
            if (observer == null)
            {
                object events = GameApi.Events(pawn);
                if (events == null) return;

                attachAttempts++;

                if (!baselineDone && (attachAttempts == 1 || attachAttempts % 120 == 0))
                {
                    Log.Write("session", "===== 核心事件基线核对（第 " + attachAttempts + " 次尝试）=====");
                    if (Baseline.Snapshot(events, true)) baselineDone = true;
                }

                var o = new InputObserver(events);
                if (o.Attach())
                {
                    observer = o;
                    Log.Write("session", "输入观察器就绪（观察模式），第 " + attachAttempts + " 次尝试成功");
                    Log.Echo("input observer attached (observe only)");
                }
                else
                {
                    o.Dispose();
                    if (attachAttempts == 1 || attachAttempts % 300 == 0)
                        Log.Write("session", "输入观察器尚未就绪（第 " + attachAttempts + " 次尝试），继续等待");
                }
            }

#if S13
            // ---- 输入接管（S1.3）----
            // 不再等 baselineDone（它原先被关在 observer==null 的块里，observer 一挂上就再也走不到，
            // 导致路由器永远不被安装）。Install() 自身会检查事件上是否有原生处理器，安全自守。
            if (router == null && ready && pawn is BGUPlayerCharacterCS)
            {
                routerAttempts++;
                var r = new McInputRouter((BGUPlayerCharacterCS)pawn, GameApi.Events(pawn));
                // 接管到的动作交给 MC 自己的行为处理
                r.OnOwnedAction = delegate (string actionName, object[] args)
                {
                    try
                    {
#if S15
                        if (actionName == "MC_Attack")
                        {
                            if (combat != null) combat.Attack();
                        }
                        else
#endif
                        if (actionName == "IA_B1Jump")
                        {
#if S15
                            if (movement != null) movement.TryJump();
#endif
                        }
                        else if (camera != null && (actionName == "IA_B1LookUp" || actionName == "IA_B1TurnRight"))
                        {
                            float x = 0f, y = 0f;
                            if (args != null && args.Length >= 2)
                            {
                                try { x = Convert.ToSingle(args[0]); y = Convert.ToSingle(args[1]); } catch { }
                            }
                            camera.AddLook(actionName, x, y);
                        }
#if S15
                        else if (movement != null && (actionName == "IA_B1MoveForward" || actionName == "IA_B1MoveRight"))
                        {
                            float x = 0f, y = 0f;
                            if (args != null && args.Length >= 2)
                            {
                                try { x = Convert.ToSingle(args[0]); y = Convert.ToSingle(args[1]); } catch { }
                            }
                            movement.SetMoveInput(actionName, x, y);
                        }
                        else if (actionName == "IA_B1Sprint_KB")
                        {
                            bool on = args != null && args.Length > 1
                                && (Convert.ToString(args[1]) == "Started" || Convert.ToString(args[1]) == "Triggered");
                            movement.SetSprint(on);
                        }
#endif
                    }
                    catch (Exception e)
                    {
                        Log.Write("route-error", "动作处理异常 " + e.GetType().Name + ": " + e.Message);
                    }
                };
                if (r.Install())
                {
                    router = r;
                    Log.Echo("input router installed (exclusive)");
                }
                else
                {
                    if (routerAttempts == 1 || routerAttempts % 300 == 0)
                        Log.Write("route", "接管尚未就绪（第 " + routerAttempts + " 次尝试）: " + r.LastError);
                }
            }
#endif
        }

        /// <summary>Pawn 的组件是否已挂全——判据是输入事件上已经有原生处理器。</summary>
        private bool IsComponentReady(AActor pawn)
        {
            try
            {
                object events = GameApi.Events(pawn);
                if (events == null) return false;
                EventHandle h = HookLedger.Resolve(events, "Evt_TriggerInputActionImpl");
                if (h == null) return false;
                var handlers = HookLedger.ReadHandlers(h);
                return handlers != null && handlers.Length > 0;
            }
            catch { return false; }
        }

        private void TryCreateCamera(AActor pawn, APlayerController controller)
        {
            cameraTried = true;
            var pc = pawn as BGUPlayerCharacterCS;
            if (pc == null)
            {
                Log.Write("camera", "当前 Pawn 不是 BGUPlayerCharacterCS，暂不建相机：" + pawn.GetClass().GetName());
                return;
            }
            camera = new McCameraRig(pc, controller);
            Log.Write("camera", "已创建相机装置（待启用）");
        }

#if S14
        private void TryCreateBody(AActor pawn)
        {
            bodyTried = true;
            var pc = pawn as BGUPlayerCharacterCS;
            if (pc == null) { Log.Write("body", "当前 Pawn 不是 BGUPlayerCharacterCS，暂不接入身体"); return; }
            try
            {
                if (bodyMaterial == null)
                {
                    bodyMaterial = UObject.LoadObject<UMaterialInterface>(null,
                        "/Engine/EngineMaterials/DefaultTextMaterialOpaque.DefaultTextMaterialOpaque");
                    if (bodyMaterial == null) { Log.Write("body-error", "引擎顶点色材质不可用"); return; }
                    bodyMaterial.AddToRoot();
                    Log.Write("body", "材质 = " + bodyMaterial.GetPathName());
                }

                string meshPath = System.IO.Path.Combine(Log.Dir, "steve.mesh");
                if (!System.IO.File.Exists(meshPath))
                {
                    Log.Write("body-error", "缺少资源文件: " + meshPath);
                    return;
                }

                body = new SteveBody(pc, bodyMaterial, meshPath);
                Log.Write("body", body.Inspect());
                Log.Echo("steve body attached");
            }
            catch (Exception e)
            {
                Log.Write("body-error", e.GetType().Name + ": " + e.Message);
                body = null;
            }
        }
#endif

#if S12
        /// <summary>
        /// 本机 AhudDump 确认 AHUD 暴露 ShowHUD 属性，PlayerController 暴露 GetHUD()。
        /// 缓存当前玩家的 HUD；游戏中每帧写 false，每秒检查一次 HUD 是否已被替换。
        /// 属性写入成功不等于所有原生 UI 都受它控制，血条/技能/道具名仍需冷启动实测。
        /// </summary>
        private void HideNativeHud(APlayerController controller)
        {
            double now = clock.Elapsed.TotalSeconds;
            try
            {
                bool cachedAlive = nativeHud != null && !nativeHud.IsDestroyed && !nativeHud.IsPendingKill;
                bool controllerChanged = nativeHudController != controller;
                if (controllerChanged || (nativeHud != null && !cachedAlive))
                    RestoreNativeHud();

                if (controllerChanged || now >= nativeHudNextRefresh)
                {
                    nativeHudNextRefresh = now + NativeHudRefreshSeconds;
                    AHUD current = controller.GetHUD();
                    if (current != null && (current.IsDestroyed || current.IsPendingKill)) current = null;
                    if (current != nativeHud)
                    {
                        RestoreNativeHud();
                        if (current != null)
                        {
                            bool originalShow = current.ShowHUD;
                            nativeHud = current;
                            nativeHudOriginalShow = originalShow;
                            nativeHudNextLog = 0;
                        }
                    }
                    nativeHudController = controller;
                    nativeHudNextRefresh = now + NativeHudRefreshSeconds;
                }

                if (nativeHud == null)
                {
                    if (now >= nativeHudNextLog)
                    {
                        nativeHudNextLog = now + NativeHudLogSeconds;
                        Log.Write("hud", "当前玩家 GetHUD() 为空，等待原生 HUD");
                    }
                    return;
                }

                // 即使上帧已经为 false 也照写，防止游戏重新开启。
                nativeHud.ShowHUD = false;
                if (now >= nativeHudNextLog)
                {
                    bool shown = nativeHud.ShowHUD;
                    nativeHudNextLog = now + NativeHudLogSeconds;
                    Log.Write("hud", "原生 HUD 每帧抑制 ShowHUD=" + shown
                        + " original=" + nativeHudOriginalShow + " type=" + nativeHud.GetType().FullName);
                }
            }
            catch (Exception e)
            {
                if (now >= nativeHudNextLog)
                {
                    nativeHudNextLog = now + NativeHudLogSeconds;
                    Log.Write("hud-error", "设置 ShowHUD 失败 " + e.GetType().Name + ": " + e.Message);
                }
            }
        }

        private void RestoreNativeHud()
        {
            try
            {
                if (nativeHud != null && !nativeHud.IsDestroyed && !nativeHud.IsPendingKill)
                {
                    nativeHud.ShowHUD = nativeHudOriginalShow;
                    Log.Write("hud", "已还原原生 HUD ShowHUD=" + nativeHudOriginalShow);
                }
            }
            catch (Exception e)
            {
                Log.Write("hud-error", "还原原生 HUD 失败 " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                nativeHud = null;
                nativeHudController = null;
                nativeHudNextRefresh = 0;
            }
        }

        /// <summary>
        /// 诊断：找出「黑猴 UI」是什么、以及用什么判据区分「加载中」与「真在游戏里」。
        /// 旧扫描发生在 Loading_TravelLevel，不能用它判断战斗 HUD 是否属于 UMG。
        /// 现在进入 InBattleStandAlone 后记录；实际抑制通过 BattleMainCon 精确定位。
        /// </summary>
        private void LogViewportWidgets(UWorld world)
        {
            try
            {
                // 1) 游戏自己的生命周期状态机 —— 用来做「是否真在游戏里」的判据
                try
                {
                    Type fsm = typeof(b1.SGI_Global);
                    string[] names = Enum.GetNames(fsm);
                    Log.Write("diag", "SGI_Global 共 " + names.Length + " 个状态: " + string.Join(",", names));

                    var manager = b1.BGW_GameLifeTimeMgr.Get(world);
                    if (manager != null)
                    {
                        foreach (string name in names)
                        {
                            bool on = false;
                            try { on = manager.IsInFSMState((b1.SGI_Global)Enum.Parse(fsm, name)); } catch { }
                            if (on) Log.Write("diag-fsm", "当前命中状态: " + name);
                        }
                    }
                    else Log.Write("diag", "GameLifeTimeMgr = null");
                }
                catch (Exception e) { Log.Write("diag-error", "FSM 查询失败 " + e.GetType().Name + ": " + e.Message); }

                // 2) 全部 UMG 控件（含不在视口的）
                int total = 0;
                foreach (UUserWidget w in UObject.GetObjects<UUserWidget>())
                {
                    if (w == null || w.IsDestroyed || w.IsPendingKill) continue;
                    bool inView = false;
                    try { inView = w.IsInViewport(); } catch { }
                    string cls;
                    try { cls = w.GetClass().GetPathName(); } catch { cls = "?"; }
                    Log.Write("diag-widget", (inView ? "[视口内] " : "[视口外] ") + cls + " vis=" + w.GetVisibility());
                    if (++total >= 80) break;
                }
                Log.Write("diag", "UUserWidget 总数 = " + total);

                // 3) AHUD —— 上一个 AI 的桥接用过 ClientSetHUD，说明黑猴 HUD 很可能是 AHUD 体系
                int huds = 0;
                foreach (AHUD h in UObject.GetObjects<AHUD>())
                {
                    if (h == null || h.IsDestroyed || h.IsPendingKill) continue;
                    string cls;
                    try { cls = h.GetClass().GetPathName(); } catch { cls = "?"; }
                    Log.Write("diag-hud", cls);
                    huds++;
                }
                Log.Write("diag", "AHUD 实例数 = " + huds);
            }
            catch (Exception e)
            {
                Log.Write("diag-error", e.GetType().Name + ": " + e.Message);
            }
        }

        private void TryCreateHud(AActor pawn, APlayerController controller)
        {
            var pc = pawn as BGUPlayerCharacterCS;
            if (pc == null) return;
            hud = new McHud(pc, controller);
            Log.Write("hud", "已创建 MC HUD（待启用）");
        }
#endif

        private void Detach()
        {
            if(bridge!=null){bridge.Dispose();bridge=null;}
            cameraEffects.Dispose();
#if S13 && S15
            if (combat != null) { combat.Dispose(); combat = null; }
            combatTried = false;
#endif
            if (camera != null) { camera.Dispose(); camera = null; }
            cameraTried = false;
#if S12
            battleHud.Dispose();
            bossHud.Dispose();
            RestoreNativeHud();
            if (hud != null) { hud.Dispose(); hud = null; }
            widgetLogged = false;
#endif
#if S13
            if (router != null) { router.Dispose(); router = null; }
            routerAttempts = 0;
#endif
#if S14
            if (body != null) { body.Dispose(); body = null; }
            bodyTried = false;
#endif
#if S15
            if (movement != null) { movement.Dispose(); movement = null; }
            McInputRouter.TakeoverMovement = false;
#endif
            if (observer != null)
            {
                Log.Write("session", "输入观察汇总: " + observer.Summary());
                observer.Dispose();
                observer = null;
            }
            sessionPawn = null;
            baselineDone = false;
            attachAttempts = 0;
        }
    }
}
