// 版本耐受的委托适配器（不依赖 System.Linq.Expressions）。
//
// 为什么需要它：游戏各版本的委托参数类型名可能变化（如 ETriggerEvent / FInputActionValue
// 的命名空间）。手写同签名方法会把整个模块钉死在参考程序集的版本上。
// 这里按"原生委托自己的 Invoke 签名"动态生成同签名委托，把参数打包成 object[] 交给处理函数。
//
// 为什么不用 Expression 树：net472 下 System.Linq.Expressions 会变成一个独立的程序集引用
// （facade），游戏用的 Mono 运行时可能解析不到 —— 实测会导致加载器 Develop 模式加载失败。
// DynamicMethod 只用 System.Reflection.Emit（在 mscorlib 里），不引入任何新引用。
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DqjCombat
{
    internal sealed class DelegateFactory
    {
        private readonly Action<object[]> handler;

        private DelegateFactory(Action<object[]> handler) { this.handler = handler; }

        /// <summary>
        /// 生成一个与 <paramref name="delegateType"/> 同签名的委托，调用时把参数打包成
        /// object[] 传给 <paramref name="handler"/>。返回值恒为该委托类型的默认值。
        /// </summary>
        internal static Delegate Create(Type delegateType, Action<object[]> handler)
        {
            if (delegateType == null) throw new ArgumentNullException("delegateType");
            return new DelegateFactory(handler).Build(delegateType);
        }

        private Delegate Build(Type delegateType)
        {
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            if (invoke == null) throw new InvalidOperationException(delegateType.Name + " 没有 Invoke");

            ParameterInfo[] ps = invoke.GetParameters();

            // 动态方法签名 = [自身(this)] + 原生参数
            var sig = new Type[ps.Length + 1];
            sig[0] = typeof(DelegateFactory);
            for (int i = 0; i < ps.Length; i++) sig[i + 1] = ps[i].ParameterType;

            var dm = new DynamicMethod(
                "DqjAdapter_" + delegateType.Name,
                invoke.ReturnType,
                sig,
                typeof(DelegateFactory).Module,
                true);

            ILGenerator il = dm.GetILGenerator();
            LocalBuilder arr = il.DeclareLocal(typeof(object[]));

            il.Emit(OpCodes.Ldc_I4, ps.Length);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Stloc, arr);

            for (int i = 0; i < ps.Length; i++)
            {
                il.Emit(OpCodes.Ldloc, arr);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);

                Type pt = sig[i + 1];
                if (pt.IsByRef)
                {
                    pt = pt.GetElementType();
                    il.Emit(OpCodes.Ldobj, pt);
                }
                if (pt.IsValueType) il.Emit(OpCodes.Box, pt);
                il.Emit(OpCodes.Stelem_Ref);
            }

            FieldInfo handlerField = typeof(DelegateFactory).GetField("handler",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo actionInvoke = typeof(Action<object[]>).GetMethod("Invoke");

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, handlerField);
            il.Emit(OpCodes.Ldloc, arr);
            il.Emit(OpCodes.Callvirt, actionInvoke);

            if (invoke.ReturnType != typeof(void))
            {
                // 注意：Action<object[]> 返回 void，Callvirt 之后栈上什么都没有，
                // 这里绝不能 Pop（会栈下溢 -> InvalidProgramException）。
                if (invoke.ReturnType.IsValueType)
                {
                    LocalBuilder ret = il.DeclareLocal(invoke.ReturnType);
                    il.Emit(OpCodes.Ldloca, ret);
                    il.Emit(OpCodes.Initobj, invoke.ReturnType);
                    il.Emit(OpCodes.Ldloc, ret);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }
            }
            il.Emit(OpCodes.Ret);

            // 绑定 this 作为动态方法的第一个参数
            return dm.CreateDelegate(delegateType, this);
        }

        /// <summary>签名字符串，便于日志。</summary>
        internal static string Signature(Type delegateType)
        {
            if (delegateType == null) return "?";
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            if (invoke == null) return "?";
            var parts = new List<string>();
            foreach (ParameterInfo p in invoke.GetParameters())
            {
                string n = p.ParameterType.IsByRef
                    ? p.ParameterType.GetElementType().Name + "&"
                    : p.ParameterType.Name;
                parts.Add(n + " " + p.Name);
            }
            return "(" + string.Join(", ", parts.ToArray()) + ")";
        }
    }
}
