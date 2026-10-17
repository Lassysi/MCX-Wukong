// 核心事件基线：安装任何钩子之前，先记录"这些事件上原本有谁"。
// 依据：S0 只读探针实测（见 S0-结论报告.md）。
// 任何一个事件的原生处理器与基线不符 → 拒绝安装，转 Suspended。
using System;
using System.Collections.Generic;
using System.Linq;

namespace DqjCombat
{
    internal sealed class ExpectedHandler
    {
        internal string TargetType;
        internal string Method;
        internal ExpectedHandler(string t, string m) { TargetType = t; Method = m; }
    }

    internal sealed class CoreEventSpec
    {
        internal string Member;
        internal ExpectedHandler[] Expected;   // S0 实测的原生处理器
        internal bool Exclusive;               // true = 上游会整体替换，必须特别小心
        internal CoreEventSpec(string member, bool exclusive, params ExpectedHandler[] expected)
        {
            Member = member; Exclusive = exclusive; Expected = expected;
        }
    }

    internal static class Baseline
    {
        private static readonly string InputComp = "b1.BUS_PlayerInputActionComp";
        private static readonly string BeAttackedComp = "b1.BUS_BeAttackedComp";
        private static readonly string AttrComp = "b1.BUS_AttrComp";
        private static readonly string TransGuideComp = "b1.BUS_TransGuideComp";

        /// <summary>S0 实测基线。Evt_IncreaseAttrFloat 有 2 个处理器，是最容易踩的坑。</summary>
        internal static readonly CoreEventSpec[] Core =
        {
            new CoreEventSpec("Evt_TriggerInputActionImpl", false,
                new ExpectedHandler(InputComp, "OnTriggerInputActionImpl")),
            new CoreEventSpec("Evt_InputCastSkill", false,
                new ExpectedHandler(InputComp, "OnInputCastSkill")),
            new CoreEventSpec("Evt_TriggerNormalDamageEffect", true,
                new ExpectedHandler(BeAttackedComp, "OnHandleNormalDamageEffect")),
            new CoreEventSpec("Evt_IncreaseAttrFloat", true,
                new ExpectedHandler(AttrComp, "OnIncreaseFloatValue"),
                new ExpectedHandler(TransGuideComp, "OnIncreaseAttrFloat")),
        };

        /// <summary>把当前状态写进日志，并返回"是否与 S0 基线一致"。</summary>
        internal static bool Snapshot(object events, bool verbose)
        {
            if (events == null) { Log.Write("baseline", "事件集合为空"); return false; }
            bool ok = true;
            int totalHandlers = 0;

            foreach (var spec in Core)
            {
                EventHandle h = HookLedger.Resolve(events, spec.Member);
                if (h == null)
                {
                    Log.Write("baseline", spec.Member + " = 无法解析（成员不存在？）");
                    ok = false;
                    continue;
                }

                var handlers = HookLedger.ReadHandlers(h);
                if (handlers == null) { ok = false; continue; }
                totalHandlers += handlers.Length;

                Log.Write("baseline", spec.Member + " " + h.Describe() + " handlers=" + handlers.Length);

                if (verbose)
                {
                    foreach (var d in handlers)
                    {
                        string t = d.Target == null ? "<static>" : d.Target.GetType().FullName;
                        Log.Write("baseline.handler", "  " + spec.Member + " <- " + t + "." + d.Method.Name);
                    }
                }

                // 逐个核对 S0 的期望
                foreach (var exp in spec.Expected)
                {
                    bool found = handlers.Any(d => d.Target != null
                        && d.Target.GetType().FullName == exp.TargetType
                        && d.Method.Name == exp.Method);
                    if (!found)
                    {
                        Log.Write("baseline.mismatch", spec.Member + " 缺少期望处理器 " + exp.TargetType + "." + exp.Method);
                        ok = false;
                    }
                }

                // 检查有没有"我们不认识"的处理器（别的模组）
                foreach (var d in handlers)
                {
                    string t = d.Target == null ? "" : d.Target.GetType().FullName;
                    if (t.StartsWith("DqjCombat") || t.StartsWith("DqjProbe")) continue;
                    bool known = spec.Expected.Any(e => e.TargetType == t && e.Method == d.Method.Name);
                    if (!known)
                        Log.Write("baseline.foreign", spec.Member + " 发现未知处理器 -> " + t + "." + d.Method.Name);
                }
            }

            if (totalHandlers == 0)
            {
                // Pawn 刚出现时组件（输入/受击/属性）可能还没挂上，事件上自然一个处理器都没有。
                // 这不是"基线偏差"，只是"还没就绪"。
                Log.Write("baseline", "结果 = 未就绪（所有核心事件都还没有原生处理器，Pawn 组件尚未挂全）");
                return false;
            }
            Log.Write("baseline", ok ? "结果 = 与 S0 基线一致" : "结果 = 存在偏差（有处理器但不符合预期）");
            return ok;
        }
    }
}
