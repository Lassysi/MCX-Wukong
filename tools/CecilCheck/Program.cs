// 离线分析模组 DLL 的 Cecil 元数据：列出所有自定义特性与类型引用，用于 diff 两个版本。
// 用法: CecilCheck <dll> [--attrs] [--refs] [--cecil] [--all]
using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0) { Console.WriteLine("用法: CecilCheck <dll> [--attrs] [--refs] [--cecil] [--all]"); return 2; }
        string path = args[0];
        if (!File.Exists(path)) { Console.WriteLine("文件不存在: " + path); return 2; }

        var modes = new HashSet<string>();
        for (int i = 1; i < args.Length; i++) modes.Add(args[i]);
        bool all = modes.Count == 0 || modes.Contains("--all");

        using (var ad = AssemblyDefinition.ReadAssembly(path))
        {
            if (all || modes.Contains("--attrs")) DumpAttributes(ad);
            if (all || modes.Contains("--refs")) DumpTypeRefs(ad);
            if (modes.Contains("--ctor")) ProbeCtorResolution(ad);
            if (modes.Contains("--trace")) { TraceResolve(path); return 0; }
            if (all || modes.Contains("--cecil")) RunCecilPipeline(ad, path);
        }
        return 0;
    }

    // ---- 自定义特性（只看 AttributeType 的 TypeRef，不解析 ctor）----
    private static void DumpAttributes(AssemblyDefinition ad)
    {
        Console.WriteLine("### 程序集级特性");
        foreach (var a in ad.CustomAttributes) Console.WriteLine("  " + a.AttributeType.FullName);
        Console.WriteLine("### 模块级特性");
        foreach (var a in ad.MainModule.CustomAttributes) Console.WriteLine("  " + a.AttributeType.FullName);
        Walk(ad.MainModule.Types);
    }

    private static void Walk(IEnumerable<TypeDefinition> types)
    {
        foreach (var t in types)
        {
            foreach (var a in t.CustomAttributes) Console.WriteLine("TYPE_ATTR " + t.FullName + " || " + a.AttributeType.FullName);
            foreach (var m in t.Methods)
                foreach (var a in m.CustomAttributes) Console.WriteLine("METH_ATTR " + t.FullName + "::" + m.Name + " || " + a.AttributeType.FullName);
            foreach (var f in t.Fields)
                foreach (var a in f.CustomAttributes) Console.WriteLine("FLD_ATTR " + t.FullName + "::" + f.Name + " || " + a.AttributeType.FullName);
            foreach (var p in t.Properties)
                foreach (var a in p.CustomAttributes) Console.WriteLine("PROP_ATTR " + t.FullName + "::" + p.Name + " || " + a.AttributeType.FullName);
            foreach (var e in t.Events)
                foreach (var a in e.CustomAttributes) Console.WriteLine("EVT_ATTR " + t.FullName + "::" + e.Name + " || " + a.AttributeType.FullName);
            Walk(t.NestedTypes);
        }
    }

    // ---- 类型引用（含所在程序集）----
    private static void DumpTypeRefs(AssemblyDefinition ad)
    {
        Console.WriteLine("### 类型引用");
        var seen = new HashSet<string>();
        foreach (var tr in ad.MainModule.GetTypeReferences())
        {
            string line = tr.FullName + "   scope=" + tr.Scope;
            if (seen.Add(line)) Console.WriteLine("TYPEREF " + line);
        }
    }

    // ---- 逐个访问特性的构造函数（这会强制 Cecil 解析声明类型）----
    private static void ProbeCtorResolution(AssemblyDefinition ad)
    {
        Console.WriteLine("### ctor 解析探测");
        int ok = 0, fail = 0;
        Probe("asm", ad.CustomAttributes, ref ok, ref fail);
        Probe("module", ad.MainModule.CustomAttributes, ref ok, ref fail);
        WalkCtor(ad.MainModule.Types, ref ok, ref fail);
        Console.WriteLine("### ctor 解析结果: ok=" + ok + " fail=" + fail);
    }

    private static void WalkCtor(IEnumerable<TypeDefinition> types, ref int ok, ref int fail)
    {
        foreach (var t in types)
        {
            Probe("type " + t.FullName, t.CustomAttributes, ref ok, ref fail);
            foreach (var m in t.Methods) Probe("method " + t.FullName + "::" + m.Name, m.CustomAttributes, ref ok, ref fail);
            foreach (var f in t.Fields) Probe("field " + t.FullName + "::" + f.Name, f.CustomAttributes, ref ok, ref fail);
            foreach (var p in t.Properties) Probe("prop " + t.FullName + "::" + p.Name, p.CustomAttributes, ref ok, ref fail);
            WalkCtor(t.NestedTypes, ref ok, ref fail);
        }
    }

    private static void Probe(string owner, IEnumerable<CustomAttribute> attrs, ref int ok, ref int fail)
    {
        foreach (var a in attrs)
        {
            string name = "?";
            try
            {
                name = a.AttributeType.FullName;
                var ctor = a.Constructor;      // 强制解析声明类型
                ok++;
                Console.WriteLine("  OK   " + owner + " || " + name + " (ctor=" + ctor.Name + ")");
            }
            catch (Exception e)
            {
                fail++;
                Console.WriteLine("  FAIL " + owner + " || " + name + " -> " + e.GetType().Name + ": " + e.Message);
            }
        }
    }

    // ---- 追踪解析：用包装好的 resolver 记录所有 Resolve 请求 ----
    private sealed class LogResolver : IMetadataResolver
    {
        private readonly IMetadataResolver inner;
        internal LogResolver(IMetadataResolver inner) { this.inner = inner; }
        public TypeDefinition Resolve(TypeReference t)
        {
            Console.WriteLine("  RESOLVE-TYPE   " + t.FullName + "   scope=" + t.Scope);
            return inner.Resolve(t);
        }
        public FieldDefinition Resolve(FieldReference f)
        {
            Console.WriteLine("  RESOLVE-FIELD  " + f.FullName);
            return inner.Resolve(f);
        }
        public MethodDefinition Resolve(MethodReference m)
        {
            Console.WriteLine("  RESOLVE-METHOD " + m.FullName + "  decl=" + m.DeclaringType.FullName);
            return inner.Resolve(m);
        }
    }

    // 模拟游戏环境：解析 mscorlib 时抛 FileNotFoundException（游戏目录下没有 mscorlib.dll）
    private sealed class TrapResolver : DefaultAssemblyResolver
    {
        public override AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            Console.WriteLine("  [ASSEMBLY-RESOLVE] " + name.FullName);
            if (name.Name == "mscorlib")
            {
                Console.WriteLine("  ---- 触发点调用栈 ----");
                Console.WriteLine(new System.Diagnostics.StackTrace(true).ToString());
                throw new FileNotFoundException("(模拟) 找不到 mscorlib.dll —— 与游戏内一致");
            }
            return base.Resolve(name);
        }
        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            Console.WriteLine("  [ASSEMBLY-RESOLVE+] " + name.FullName);
            if (name.Name == "mscorlib")
                throw new FileNotFoundException("(模拟) 找不到 mscorlib.dll —— 与游戏内一致");
            return base.Resolve(name, parameters);
        }
    }

    private static void TraceResolve(string path)
    {
        Console.WriteLine("### 解析追踪（复现加载器 Develop 分支 + 模拟游戏缺少 mscorlib）");
        var rp = new ReaderParameters { AssemblyResolver = new TrapResolver() };
        using (var ad = AssemblyDefinition.ReadAssembly(path, rp))
        {
            ad.Name.Name += "_trace";
            try
            {
                using (var ms = new MemoryStream())
                {
                    ad.Write(ms);
                    Console.WriteLine("### Write 完成 " + ms.Length + " bytes（追踪结束）");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("### Write 失败 " + e.GetType().Name + ": " + e.Message);
            }
        }
    }

    // ---- 复现加载器的 Develop 分支 ----
    private static void RunCecilPipeline(AssemblyDefinition ad, string path)
    {
        Console.WriteLine("### Cecil 管线复现");
        try
        {
            ad.Name.Name += "_cecilcheck";
            using (var ms = new MemoryStream())
            {
                ad.Write(ms);
                Console.WriteLine("[OK] ReadAssembly + 重命名 + Write 通过 -> " + ms.Length + " bytes");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("[FAIL] " + e.GetType().FullName + ": " + e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }
}
