// 钩子设施：事件成员的解析、处理器读取、安装与卸载。
// 机制照抄上游 wukong-steve（已验证过的写法）：
//   - 事件成员的值是一个包装对象，内部有 _InvokeCallBack 或 _MultiCastDel 字段（Delegate）
//   - 安装/卸载用 Delegate.Combine / Delegate.Remove 操作该字段
//   - 卸载公式：Combine(native, Remove(current, mine))，绝不整体覆盖字段
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BtlShare;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;
using b1;
using b1.EventDelDefine;

namespace DqjCombat
{
    internal enum HookMode
    {
        Unknown = 0,
        EventAccessor,      // C# 事件：add_/remove_
        DelegatingProperty, // 属性类型本身就是委托，且有 setter
        InnerDelegate       // 包装对象内的 _InvokeCallBack / _MultiCastDel
    }

    internal sealed class EventHandle
    {
        internal string Member;
        internal object Collection;
        internal object Signal;            // 包装对象（InnerDelegate 模式）
        internal HookMode Mode;

        internal MethodInfo Add, Remove;   // EventAccessor
        internal MethodInfo Getter, Setter, OpAdd, OpSub; // DelegatingProperty
        internal FieldInfo InnerField;     // InnerDelegate

        internal string Describe()
        {
            string extra = "";
            if (Mode == HookMode.InnerDelegate && InnerField != null) extra = " field=" + InnerField.Name;
            return Member + " [" + Mode + extra + "]";
        }
    }

    internal sealed class HookRecord
    {
        internal EventHandle Handle;
        internal Delegate Native;
        internal Delegate Mine;
        internal bool Installed;
        internal bool Fired;   // 我们的回调是否真的被游戏调用过
        internal string Error;

        internal string Describe()
        {
            return Handle.Member
                + " installed=" + Installed
                + " fired=" + Fired
                + " native=" + (Native == null ? "?" : Native.Target.GetType().Name + "." + Native.Method.Name)
                + (Error == null ? "" : " error=" + Error);
        }
    }

    internal static class HookLedger
    {
        private static readonly BindingFlags Any =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // ---------------- 解析 ----------------

        internal static EventHandle Resolve(object collection, string member)
        {
            if (collection == null) return null;
            Type t = collection.GetType();
            var h = new EventHandle { Member = member, Collection = collection };

            // 1) C# 事件
            try
            {
                var ev = t.GetEvent(member, Any);
                if (ev != null && ev.AddMethod != null && ev.RemoveMethod != null)
                {
                    h.Mode = HookMode.EventAccessor;
                    h.Add = ev.AddMethod;
                    h.Remove = ev.RemoveMethod;
                    return h;
                }
            }
            catch { }

            // 2) 属性（包装对象 / 委托属性）
            try
            {
                var pi = t.GetProperty(member, Any);
                if (pi != null && pi.GetIndexParameters().Length == 0 && pi.CanRead)
                {
                    object val = null;
                    try { val = pi.GetValue(collection, null); } catch { }
                    if (val != null)
                    {
                        FieldInfo inner = FindInnerDelegateField(val.GetType());
                        if (inner != null)
                        {
                            h.Mode = HookMode.InnerDelegate;
                            h.Signal = val;
                            h.InnerField = inner;
                            return h;
                        }
                        if (pi.CanWrite && typeof(Delegate).IsAssignableFrom(pi.PropertyType))
                        {
                            h.Mode = HookMode.DelegatingProperty;
                            h.Getter = pi.GetGetMethod(true);
                            h.Setter = pi.GetSetMethod(true);
                            h.Signal = val;
                            return h;
                        }
                    }
                }
            }
            catch { }

            // 3) 字段
            try
            {
                var fi = t.GetField(member, Any);
                if (fi != null)
                {
                    object val = fi.GetValue(collection);
                    if (val != null)
                    {
                        FieldInfo inner = FindInnerDelegateField(val.GetType());
                        if (inner != null)
                        {
                            h.Mode = HookMode.InnerDelegate;
                            h.Signal = val;
                            h.InnerField = inner;
                            return h;
                        }
                    }
                    if (typeof(Delegate).IsAssignableFrom(fi.FieldType))
                    {
                        h.Mode = HookMode.InnerDelegate;
                        h.Signal = collection;
                        h.InnerField = fi;
                        return h;
                    }
                }
            }
            catch { }

            return null;
        }

        private static FieldInfo FindInnerDelegateField(Type wrapper)
        {
            foreach (string name in new[] { "_InvokeCallBack", "_MultiCastDel" })
            {
                try
                {
                    var f = wrapper.GetField(name, Any);
                    if (f != null && typeof(Delegate).IsAssignableFrom(f.FieldType)) return f;
                }
                catch { }
            }
            // 兜底：唯一的委托字段
            try
            {
                var all = wrapper.GetFields(Any).Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType)).ToArray();
                if (all.Length == 1) return all[0];
            }
            catch { }
            return null;
        }

        // ---------------- 读取 ----------------

        internal static Delegate[] ReadHandlers(EventHandle h)
        {
            if (h == null) return null;
            try
            {
                Delegate d = null;
                if (h.Mode == HookMode.InnerDelegate)
                {
                    d = h.InnerField.GetValue(h.Signal) as Delegate;
                }
                else if (h.Mode == HookMode.DelegatingProperty)
                {
                    d = h.Getter.Invoke(h.Collection, null) as Delegate;
                }
                else if (h.Mode == HookMode.EventAccessor)
                {
                    // C# 事件的背衬字段通常与成员同名
                    var f = h.Collection.GetType().GetField(h.Member,
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (f != null) d = f.GetValue(h.Collection) as Delegate;
                }
                return d == null ? new Delegate[0] : d.GetInvocationList();
            }
            catch (Exception e)
            {
                Log.Write("hook-read-error", h.Member + " " + e.GetType().Name + ": " + e.Message);
                return null;
            }
        }

        internal static string DescribeHandlers(EventHandle h)
        {
            var list = ReadHandlers(h);
            if (list == null) return "(读取失败)";
            if (list.Length == 0) return "0 个";
            return list.Length + " 个: " + string.Join(", ",
                list.Select(d => (d.Target == null ? "<static>" : d.Target.GetType().Name) + "." + d.Method.Name));
        }

        // ---------------- 安装 / 卸载 ----------------

        internal static bool Install(EventHandle h, Delegate mine, out string error)
        {
            error = null;
            if (h == null) { error = "句柄为空"; return false; }
            try
            {
                switch (h.Mode)
                {
                    case HookMode.EventAccessor:
                        h.Add.Invoke(h.Collection, new object[] { mine });
                        return true;

                    case HookMode.InnerDelegate:
                    {
                        var cur = h.InnerField.GetValue(h.Signal) as Delegate;
                        h.InnerField.SetValue(h.Signal, Delegate.Combine(cur, mine));
                        return true;
                    }

                    case HookMode.DelegatingProperty:
                    {
                        var cur = h.Getter.Invoke(h.Collection, null) as Delegate;
                        h.Setter.Invoke(h.Collection, new object[] { Delegate.Combine(cur, mine) });
                        return true;
                    }
                }
                error = "未知模式";
                return false;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        /// <summary>卸载：只摘掉自己，绝不覆盖字段，绝不恢复旧快照。</summary>
        internal static bool Uninstall(EventHandle h, Delegate mine, out string error)
        {
            error = null;
            if (h == null) { error = "句柄为空"; return false; }
            try
            {
                switch (h.Mode)
                {
                    case HookMode.EventAccessor:
                        h.Remove.Invoke(h.Collection, new object[] { mine });
                        return true;

                    case HookMode.InnerDelegate:
                    {
                        var cur = h.InnerField.GetValue(h.Signal) as Delegate;
                        if (cur == null) return true;
                        var next = Delegate.Remove(cur, mine);
                        h.InnerField.SetValue(h.Signal, next);
                        return true;
                    }

                    case HookMode.DelegatingProperty:
                    {
                        var cur = h.Getter.Invoke(h.Collection, null) as Delegate;
                        if (cur == null) return true;
                        h.Setter.Invoke(h.Collection, new object[] { Delegate.Remove(cur, mine) });
                        return true;
                    }
                }
                error = "未知模式";
                return false;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        /// <summary>当前处理器里是否存在目标原生处理器（用来判断"别人动了没有"）。</summary>
        internal static bool HasHandler(EventHandle h, string targetTypeName, string methodName)
        {
            var list = ReadHandlers(h);
            if (list == null) return false;
            return list.Any(d => d.Target != null
                && d.Target.GetType().FullName == targetTypeName
                && d.Method.Name == methodName);
        }
    }
}
