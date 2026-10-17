// S1.0：输入观察器。
// 只在低层输入事件上挂一个"只读"钩子，记录玩家按了什么。
// 本阶段【不消费】任何动作 —— 原版行为完全不受影响，风险为零。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DqjCombat
{
    internal sealed class InputObserver : IDisposable
    {
        private static readonly string Member = "Evt_TriggerInputActionImpl";

        private readonly object events;
        private HookRecord record;
        private int count;
        private readonly Dictionary<string, int> byAction = new Dictionary<string, int>();

        internal bool Hooked { get { return record != null && record.Installed; } }
        internal bool Verified { get { return record != null && record.Fired; } }
        internal int Count { get { return count; } }

        internal InputObserver(object eventCollection)
        {
            events = eventCollection;
        }

        internal bool Attach()
        {
            if (events == null) { Log.Write("input", "事件集合为空，放弃"); return false; }

            EventHandle h = HookLedger.Resolve(events, Member);
            if (h == null) { Log.Write("input", Member + " 无法解析"); return false; }

            var handlers = HookLedger.ReadHandlers(h);

            // Pawn 刚出现时组件可能还没挂上，事件上暂时没有原生处理器。
            // 这不是错误，也不能挂钩子，等下一帧重试即可。
            if (handlers == null || handlers.Length == 0)
            {
                Log.Write("input", "未就绪：" + Member + " 当前没有原生处理器（Pawn 组件可能尚未挂上），稍后重试");
                return false;
            }

            Log.Write("input", "安装前 " + h.Describe() + " handlers=" + handlers.Length);
            Log.Write("input", "原生签名 " + DelegateFactory.Signature(handlers[0].GetType()));

            Delegate mine;
            try
            {
                mine = DelegateFactory.Create(handlers[0].GetType(), OnRaw);
            }
            catch (Exception e)
            {
                Log.Write("input-error", "生成适配委托失败 " + e.GetType().Name + ": " + e.Message);
                return false;
            }

            string err;
            bool ok = HookLedger.Install(h, mine, out err);
            record = new HookRecord { Handle = h, Mine = mine, Installed = ok, Error = err };
            if (ok)
            {
                record.Native = handlers[0];
                Log.Write("input", "已安装（观察模式，不消费）→ " + h.Describe());
                Log.Write("input", "安装后 handlers=" + HookLedger.DescribeHandlers(h));
            }
            else
            {
                Log.Write("input-error", "安装失败: " + err);
            }
            return ok;
        }

        private void OnRaw(object[] args)
        {
            try
            {
                if (record != null) record.Fired = true;
                count++;

                string name = args.Length > 0 ? Convert.ToString(args[0]) : "?";
                string trigger = args.Length > 1 ? Convert.ToString(args[1]) : "?";

                int n;
                byAction.TryGetValue(name, out n);
                byAction[name] = n + 1;

                // 只记录前 200 条明细，避免刷爆日志
                if (count <= 200)
                    Log.Write("input", "#" + count + " " + name + " / " + trigger);
            }
            catch (Exception e)
            {
                Log.Write("input-error", "回调内异常 " + e.GetType().Name + ": " + e.Message);
            }
        }

        internal string Summary()
        {
            var top = byAction.OrderByDescending(kv => kv.Value).Take(30)
                .Select(kv => kv.Key + "=" + kv.Value);
            return "总数=" + count + " | " + string.Join(", ", top);
        }

        public void Dispose()
        {
            if (record == null || !record.Installed) return;
            string err;
            bool ok = HookLedger.Uninstall(record.Handle, record.Mine, out err);
            Log.Write("input", "卸载 " + (ok ? "成功" : "失败 " + err));
            Log.Write("input", "卸载后 handlers=" + HookLedger.DescribeHandlers(record.Handle));

            var after = HookLedger.ReadHandlers(record.Handle);
            if (after != null)
            {
                bool clean = !after.Any(d => d.Target != null
                    && d.Target.GetType().FullName != null
                    && d.Target.GetType().FullName.StartsWith("DqjCombat"));
                Log.Write("input", "残留检查 = " + (clean ? "干净" : "仍有本模组处理器"));
            }
            record.Installed = false;
        }
    }
}
