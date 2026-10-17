// S1.3：输入接管路由器（独占模式）
//
// 做法：把原生 OnTriggerInputActionImpl 从事件上摘下，装上我们自己的处理器；
// 我们的处理器决定哪些动作透传给原生、哪些由本模组接管（消费）。
//
// 与 S1.0 观察器的区别：观察器只是挂在旁边看（原版照常工作，不消费）；
// 路由器是**独占**的——原生处理器必须先摘掉，否则一次按键会被处理两次。
//
// 接管动作：跳跃 / 视角（鼠标）/ 移动（前后左右）/ 疾跑 —— 全部交给 MC 自己的行为。
// 注意（见 模组开发雷区.md）：本文件不得出现 const 字段。
using System;
using System.Collections.Generic;
using System.Reflection;
using b1;
using BtlShare;
using UnrealEngine.Engine;
using UnrealEngine.Plugins.EnhancedInput;

namespace DqjCombat
{
    internal sealed class McInputRouter : IDisposable
    {
        private static readonly string EventMember = "Evt_TriggerInputActionImpl";
        private static readonly string NativeType = "b1.BUS_PlayerInputActionComp";
        private static readonly string NativeMethod = "OnTriggerInputActionImpl";

        // 由本模组接管的动作名（按上一轮实测的 Enhanced Input 动作名精确匹配）
        private static readonly string[] OwnedNames =
        {
            "IA_B1Jump", "IA_B1LookUp", "IA_B1TurnRight",
            "IA_B1MoveForward", "IA_B1MoveRight", "IA_B1Sprint_KB"
        };

        // 移动接管的总开关：只有确认角色用标准 CMC 时（McMovement.Active）才置 true，
        // 否则把移动动作透传给原生，避免把角色「冻住」（CMC 不可用时消费移动会完全不动）。
        internal static bool TakeoverMovement = false;
        internal bool GameplayActive;
        internal bool TakeoverCombat;
        internal bool TakeoverBridge;
        internal Func<bool> BridgeHasScreen;
        internal Action<Action> BridgeNativeInteraction;
        private readonly Dictionary<string,bool> nativeInteractActions=new Dictionary<string,bool>();

        /// <summary>本模组接管了某个动作时的回调（用于执行 MC 自己的行为）。</summary>
        internal Action<string, object[]> OnOwnedAction;

        private readonly BGUPlayerCharacterCS player;
        private readonly object events;

        private EventHandle handle;
        private Delegate original;
        private Delegate replacement;
        private bool installed;
        private string lastError;

        private int seen;
        private readonly Dictionary<string, int> typeCounts = new Dictionary<string, int>();
        private bool valueLogged;

        internal bool Installed { get { return installed; } }
        internal string LastError { get { return lastError; } }

        internal McInputRouter(BGUPlayerCharacterCS player, object eventCollection)
        {
            this.player = player;
            events = eventCollection;
        }

        internal bool Install()
        {
            if (installed) return true;
            lastError = null;
            try
            {
                if (events == null) { lastError = "事件集合为空"; return false; }

                handle = HookLedger.Resolve(events, EventMember);
                if (handle == null) { lastError = "无法解析 " + EventMember; return false; }

                Delegate[] handlers = HookLedger.ReadHandlers(handle);
                if (handlers == null || handlers.Length == 0)
                {
                    lastError = "尚未就绪：事件上没有原生处理器";
                    return false;
                }

                Delegate native = null;
                foreach (Delegate d in handlers)
                {
                    if (d.Target == null) continue;
                    if (d.Target.GetType().FullName != NativeType) continue;
                    if (d.Method.Name != NativeMethod) continue;
                    native = d;
                    break;
                }
                if (native == null) { lastError = "找不到预期的原生处理器 " + NativeType + "." + NativeMethod; return false; }

                original = native;
                replacement = DelegateFactory.Create(native.GetType(), OnRaw);

                // 先摘原生、再装我们（顺序不能反，否则一瞬间会有两个处理器）
                string err;
                if (!HookLedger.Uninstall(handle, original, out err))
                {
                    lastError = "摘除原生处理器失败: " + err;
                    return false;
                }
                if (!HookLedger.Install(handle, replacement, out err))
                {
                    HookLedger.Install(handle, original, out err);   // 回滚
                    lastError = "安装路由器失败: " + err;
                    return false;
                }

                installed = true;
                Log.Write("route", "输入路由器已接管（独占模式） handlers=" + HookLedger.DescribeHandlers(handle));
                Log.Echo("input router installed (exclusive)");
                return true;
            }
            catch (Exception e)
            {
                lastError = e.GetType().Name + ": " + e.Message;
                Log.Write("route-error", "安装失败 " + lastError);
                return false;
            }
        }

        // ---------------------------------------------------------------- 分发

        private void OnRaw(object[] args)
        {
            seen++;
            string name = args.Length > 0 ? Convert.ToString(args[0]) : "?";
            string trigger = args.Length > 1 ? Convert.ToString(args[1]) : "?";
            LogValueShapeOnce(args);

            int n;
            typeCounts.TryGetValue(name, out n);
            typeCounts[name] = n + 1;

            bool gameplay = GameplayActive && (TakeoverBridge || IsGameplay());
            if(gameplay && TakeoverBridge)
            {
                if(BridgeHasScreen!=null&&BridgeHasScreen()&&name.StartsWith("IA_B1",StringComparison.Ordinal))return;
                if(BridgeNativeInteraction!=null&&IsNativeInteract(name,args)){
                    if(trigger=="Started"){
                        Log.Write("shrine-input","native interaction press: "+name+" / "+trigger);
                        BridgeNativeInteraction(()=>original.DynamicInvoke(args));
                    }
                    return;
                }
                // The real MC client consumes keyboard/mouse through the bridge.
                // Preserve native menu/pause navigation; suppress world actions.
                if(name.StartsWith("IA_B1",StringComparison.Ordinal)
                    && name.IndexOf("Pause",StringComparison.OrdinalIgnoreCase)<0
                    && name.IndexOf("Menu",StringComparison.OrdinalIgnoreCase)<0)return;
            }
            bool owned = IsOwned(name);
            bool combatAction = false;
            bool blockedNative = false;
            if (gameplay && TakeoverCombat)
            {
                // 用游戏描述表判定语义，覆盖不同键位绑定的轻击/重击/技能/锁定等动作。
                try
                {
                    var eventType = args.Length > 1 && args[1] is ETriggerEvent
                        ? (ETriggerEvent)args[1] : ETriggerEvent.None;
                    var action = BGW_GameDB.GetDescByInputActionNameAndTriggerEvent(player.GetResID(), name, eventType);
                    if (action != null)
                    {
                        combatAction = action.InputActionType == EInputActionType.LightAttack;
                        blockedNative = action.InputActionType != EInputActionType.Interact
                            && action.InputActionType != EInputActionType.None;
                    }
                }
                catch (Exception e) { if (seen <= 160) Log.Write("route-error", "动作描述: " + e.Message); }
                // 这些原始动作已在本机日志确认；描述表空值也不能漏回原版攻击。
                if (name == "IA_B1LightAttack") combatAction = true;
                if (name == "IA_B1HeavyAttack" || name == "IA_B1SkillDrag") blockedNative = true;
            }
            if (!TakeoverMovement && (name == "IA_B1Jump" || name == "IA_B1MoveForward"
                || name == "IA_B1MoveRight" || name == "IA_B1Sprint_KB")) owned = false;
            if (seen <= 160)
                Log.Write("route", "#" + seen + " " + name + " / " + trigger
                    + (gameplay && (owned || combatAction || blockedNative) ? "  [MC消费]" : "  [透传原生]"));

            bool handled = false;
            if (gameplay && combatAction)
            {
                if (trigger == "Started" && OnOwnedAction != null) OnOwnedAction("MC_Attack", args);
                handled = true;
            }
            else if (owned && gameplay)
            {
                // 视角 / 移动需要轴值：读得到就接管，读不到就透传（保证镜头/移动不失效）
                if (name == "IA_B1LookUp" || name == "IA_B1TurnRight"
                    || name == "IA_B1MoveForward" || name == "IA_B1MoveRight")
                {
                    float x, y;
                    if (TryReadAxis2D(args, out x, out y))
                    {
                        try { if (OnOwnedAction != null) OnOwnedAction(name, new object[] { x, y }); } catch { }
                        handled = true;
                    }
                    // 读不到轴值 → 落到下面透传给原生
                }
                else
                {
                    // 跳跃、疾跑直接读取 Space/Ctrl，避免与原生动作模板重复驱动。
                    handled = true;
                }
            }

            if (gameplay && blockedNative) handled = true;

            if (!handled)
            {
                try { if (original != null) original.DynamicInvoke(args); }
                catch (Exception e) { Log.Write("route-error", "透传失败 " + e.GetType().Name + ": " + e.Message); }
            }
        }

        private bool IsNativeInteract(string name,object[] args)
        {
            string key=name+"/"+(args.Length>1?args[1].ToString():"");bool value;
            if(nativeInteractActions.TryGetValue(key,out value))return value;
            try{
                var trigger=args.Length>1&&args[1] is ETriggerEvent?(ETriggerEvent)args[1]:ETriggerEvent.None;
                var desc=BGW_GameDB.GetDescByInputActionNameAndTriggerEvent(player.GetResID(),name,trigger);
                value=desc!=null&&desc.InputActionType==EInputActionType.Interact;
            }catch(Exception e){value=false;Log.Write("shrine-input-error","native action classification "+key+": "+e.Message);}
            nativeInteractActions[key]=value;return value;
        }

        internal void EnsureAttached()
        {
            if (!installed || handle == null) return;
            try
            {
                Delegate[] handlers = HookLedger.ReadHandlers(handle);
                bool hasReplacement = false;
                foreach (Delegate d in handlers)
                {
                    if (d == replacement) hasReplacement = true;
                    if (d.Target == null || d.Target.GetType().FullName != NativeType || d.Method.Name != NativeMethod) continue;
                    string error;
                    if (HookLedger.Uninstall(handle, d, out error))
                    {
                        original = d;
                        Log.Write("route", "移除游戏重新登记的原生输入处理器");
                    }
                }
                if (!hasReplacement)
                {
                    string error;
                    if (!HookLedger.Install(handle, replacement, out error)) Log.Write("route-error", "重挂失败: " + error);
                }
            }
            catch (Exception e) { Log.Write("route-error", "核对接管: " + e.Message); }
        }

        private bool IsOwned(string name)
        {
            if (name == null) return false;
            for (int i = 0; i < OwnedNames.Length; i++)
                if (OwnedNames[i] == name) return true;
            return false;
        }

        /// <summary>菜单/暂停/有鼠标光标时不接管，避免吃掉 UI 操作。</summary>
        private bool IsGameplay()
        {
            try
            {
                if (shortPaused()) return false;
                if (player.IsDestroyed || player.IsPendingKill) return false;
                APlayerController pc = player.GetController() as APlayerController;
                if (pc == null) return false;
                if (pc.IsMoveInputIgnored()) return false;
                if (pc.ShowMouseCursor) return false;
                return true;
            }
            catch { return false; }
        }

        private bool shortPaused()
        {
            try { return UnrealEngine.Engine.UGameplayStatics.IsGamePaused(player); } catch { return false; }
        }

        /// <summary>一次性记录输入动作值结构，用于实现 MC 自己的鼠标视角。</summary>
        private void LogValueShapeOnce(object[] args)
        {
            if (valueLogged || args.Length < 3 || args[2] == null) return;
            valueLogged = true;
            try
            {
                Type t = args[2].GetType();
                var parts = new List<string>();
                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (p.GetIndexParameters().Length != 0) continue;
                    try { parts.Add(p.Name + "=" + p.GetValue(args[2], null)); } catch { }
                }
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    try { parts.Add(f.Name + "=" + f.GetValue(args[2])); } catch { }
                }
                Log.Write("route-value", t.FullName + " :: " + string.Join(", ", parts.ToArray()));
            }
            catch { }
        }

        /// <summary>
        /// 从动作值（第 3 个参数）读取 2D 轴值。
        /// 实测 `b1.FInputActionValue` 的真实值在内部字段 `_value` 里（FVector/Value 结构），
        /// 不在 `FInputActionValue` 本身；之前读 X/Y 直接字段拿到 0，导致视角/移动都不动。
        /// 兼容只有 X 的一维轴。
        /// </summary>
        private bool TryReadAxis2D(object[] args, out float x, out float y)
        {
            x = 0f; y = 0f;
            if (args.Length < 3 || args[2] == null) return false;
            try
            {
                object v = args[2];
                Type t = v.GetType();

                // 1) 优先读内部真实值字段：_value / Value / value
                object inner = ReadMemberObject(v, t, "_value");
                if (inner == null) inner = ReadMemberObject(v, t, "Value");
                if (inner == null) inner = ReadMemberObject(v, t, "value");
                if (inner == null) inner = v; // fallback：直接读 v 的 X/Y

                Type it = inner.GetType();
                float fx = ReadMember(inner, it, "X");
                float fy = ReadMember(inner, it, "Y");
                x = float.IsNaN(fx) ? 0f : fx;
                y = float.IsNaN(fy) ? 0f : fy;
                if ((float.IsNaN(fx) && float.IsNaN(fy)) || float.IsInfinity(x) || float.IsInfinity(y)) return false;
                return true;
            }
            catch { return false; }
        }

        private static object ReadMemberObject(object v, Type t, string name)
        {
            try
            {
                PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null && p.CanRead) return p.GetValue(v, null);
                FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) return f.GetValue(v);
            }
            catch { }
            return null;
        }

        private static float ReadMember(object v, Type t, string name)
        {
            try
            {
                PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null && p.CanRead) return Convert.ToSingle(p.GetValue(v, null));
                FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) return Convert.ToSingle(f.GetValue(v));
            }
            catch { }
            return float.NaN;
        }

        internal string Summary()
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> kv in typeCounts)
                parts.Add(kv.Key + "=" + kv.Value);
            return "路由事件=" + seen + " | " + string.Join(", ", parts.ToArray());
        }

        // ---------------------------------------------------------------- 卸载

        public void Dispose()
        {
            if (!installed) return;
            try
            {
                string err;
                HookLedger.Uninstall(handle, replacement, out err);
                if (original != null) HookLedger.Install(handle, original, out err);
                Log.Write("route", "已还原原生输入处理器");
                Log.Write("route", "还原后 handlers=" + HookLedger.DescribeHandlers(handle));
                Log.Write("route", "汇总: " + Summary());
            }
            catch (Exception e)
            {
                Log.Write("route-error", "还原失败 " + e.GetType().Name + ": " + e.Message);
            }
            installed = false;
            handle = null;
            original = null;
            replacement = null;
        }
    }
}
