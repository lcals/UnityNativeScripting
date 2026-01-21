using System.Text;
using System.Text.RegularExpressions;

static class Program
{
    private sealed record CsModule(string Module, string CsNamespace, ApiModel Model);

    private static int Main(string[] args)
    {
        string repoRoot = FindRepoRoot();
        var apiFiles = GetArgs(args, "--api");
        if (apiFiles.Count == 0)
        {
            string defsDir = Path.Combine(repoRoot, "Tests", "defs");
            if (Directory.Exists(defsDir))
            {
                apiFiles.AddRange(Directory.GetFiles(defsDir, "*.def", SearchOption.TopDirectoryOnly));
                apiFiles.Sort(StringComparer.OrdinalIgnoreCase);
            }
            if (apiFiles.Count == 0)
                throw new InvalidOperationException($"未找到任何 .def 文件：{defsDir}。请创建 Tests/defs/*.def 或使用 --api 指定输入。");
        }

        // outCpp 约定为 Tests 目录（默认：<repoRoot>/Tests），所有模块输出到 Tests/cpp/generated
        string outCpp = GetArg(args, "--out-cpp") ?? Path.Combine(repoRoot, "Tests");
        string outCs = GetArg(args, "--out-cs") ?? Path.Combine(repoRoot, "Tests", "csharp", "RobotHost", "Generated");

        string? singleModuleOverride = GetArg(args, "--module");
        string? singleNamespaceOverride = GetArg(args, "--cs-namespace");
        bool clean = !string.Equals(GetArg(args, "--clean"), "false", StringComparison.OrdinalIgnoreCase);

        Directory.CreateDirectory(outCpp);
        Directory.CreateDirectory(outCs);

        var usedHostIds = new Dictionary<uint, string>();
        var usedCoreIds = new Dictionary<uint, string>();
        var csModules = new List<CsModule>();

        foreach (string apiFile in apiFiles)
        {
            string fullApiFile = Path.IsPathRooted(apiFile) ? apiFile : Path.Combine(repoRoot, apiFile);
            var model = ApiModel.Parse(File.ReadAllText(fullApiFile));

            string module = singleModuleOverride ?? ModuleNameFromApiPath(fullApiFile);
            string cppNs = CppNamespaceFromModule(module);
            string csNs = singleNamespaceOverride ?? $"{module}.Bindings";

            foreach (var fn in model.HostFns)
                RegisterIdOrThrow(usedHostIds, ComputeHostFuncId(module, fn.Name), $"H:{module}.{fn.Name}");

            foreach (var fn in model.CoreFns)
                RegisterIdOrThrow(usedCoreIds, ComputeCoreFuncId(module, fn.Name), $"C:{module}.{fn.Name}");

            string outCppDir = ResolveOutCppDir(repoRoot, outCpp, module);
            Directory.CreateDirectory(outCppDir);

            if (clean)
                CleanupGeneratedCs(outCs, module);

            string cppFileName = $"{cppNs}_bindings.generated.h";
            File.WriteAllText(Path.Combine(outCppDir, cppFileName), CppEmitter.Emit(model, module, cppNs), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            foreach (var file in CsEmitter.EmitFiles(model, module, csNs))
            {
                File.WriteAllText(Path.Combine(outCs, file.FileName), file.Contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            Console.WriteLine($"OK: {fullApiFile}");
            Console.WriteLine($"  C++: {outCppDir}");
            Console.WriteLine($"  C#:  {outCs}");

            csModules.Add(new CsModule(module, csNs, model));
        }

        foreach (var file in CsEmitter.EmitAggregateFiles(csModules))
        {
            File.WriteAllText(Path.Combine(outCs, file.FileName), file.Contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        return 0;
    }

    private static void RegisterIdOrThrow(Dictionary<uint, string> map, uint id, string name)
    {
        if (map.TryGetValue(id, out string? existing) && !string.Equals(existing, name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"FuncId 冲突：0x{id:X8} 同时用于 `{existing}` 与 `{name}`。请重命名或拆分模块。");
        }
        map[id] = name;
    }

    private static uint ComputeHostFuncId(string module, string fnName)
    {
        return Fnv1a32("H:" + module + "." + fnName);
    }

    private static uint ComputeCoreFuncId(string module, string fnName)
    {
        return Fnv1a32("C:" + module + "." + fnName);
    }

    private static uint Fnv1a32(string s)
    {
        const uint offset = 2166136261u;
        const uint prime = 16777619u;

        uint hash = offset;
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= prime;
        }
        return hash;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
                return args[i + 1];
        }
        return null;
    }

    private static List<string> GetArgs(string[] args, string name)
    {
        var list = new List<string>();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
                list.Add(args[i + 1]);
        }
        return list;
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            if (File.Exists(Path.Combine(dir, "CMakeLists.txt")))
                return dir;

            string? parent = Directory.GetParent(dir)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
                break;
            dir = parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static void CleanupGeneratedCs(string outCsDir, string module)
    {
        if (!Directory.Exists(outCsDir))
            return;

        // 仅清理本模块生成的文件，避免误删其他模块
        string prefix = module + ".";
        string hostApiFile = "I" + module + "HostApi.g.cs";
        foreach (string file in Directory.GetFiles(outCsDir, "*.g.cs", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith(prefix, StringComparison.Ordinal) ||
                string.Equals(name, hostApiFile, StringComparison.Ordinal) ||
                string.Equals(name, module + "Bindings.g.cs", StringComparison.Ordinal))
            {
                File.Delete(file);
            }
        }
    }

    private static string ModuleNameFromApiPath(string apiPath)
    {
        string name = Path.GetFileNameWithoutExtension(apiPath);
        if (name.EndsWith("_api", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 4);

        var parts = name.Split(new[] { '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (string p in parts)
        {
            if (p.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p.Substring(1));
        }
        return sb.Length == 0 ? "DemoGame" : sb.ToString();
    }

    private static string CppNamespaceFromModule(string module)
    {
        // DemoGame -> demo_game
        var sb = new StringBuilder();
        for (int i = 0; i < module.Length; i++)
        {
            char c = module[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static string ResolveOutCppDir(string repoRoot, string outCppArg, string module)
    {
        // 默认 outCpp 为 Tests；所有模块输出到 Tests/cpp/generated（集中一个目录，便于 CMake include）
        string outCppRoot = Path.IsPathRooted(outCppArg) ? outCppArg : Path.Combine(repoRoot, outCppArg);
        return Path.Combine(outCppRoot, "cpp", "generated");
    }

    private sealed record ApiModel(List<ApiFn> HostFns, List<ApiFn> CoreFns)
    {
        public static ApiModel Parse(string text)
        {
            var hostFns = new List<ApiFn>();
            var coreFns = new List<ApiFn>();

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (TryParseMacro(line, "BRIDGE_HOST_API", out ApiFn hostFn))
                {
                    hostFns.Add(hostFn);
                    continue;
                }

                if (TryParseMacro(line, "BRIDGE_CORE_API", out ApiFn coreFn))
                {
                    coreFns.Add(coreFn);
                    continue;
                }
            }

            return new ApiModel(hostFns, coreFns);
        }

        private static bool TryParseMacro(string line, string macroName, out ApiFn fn)
        {
            fn = new ApiFn(string.Empty, new List<ApiArg>());

            var match = Regex.Match(line, $"^{Regex.Escape(macroName)}\\((.*)\\)\\s*$");
            if (!match.Success)
                return false;

            string inside = match.Groups[1].Value.Trim();
            var parts = SplitTopLevel(inside);
            if (parts.Count < 1)
                throw new InvalidOperationException($"无效定义：{line}");

            string name = parts[0].Trim();
            var args = new List<ApiArg>();
            for (int i = 1; i < parts.Count; i++)
            {
                string arg = parts[i].Trim();
                if (arg.Length == 0)
                    continue;

                int lastSpace = arg.LastIndexOf(' ');
                if (lastSpace <= 0 || lastSpace == arg.Length - 1)
                    throw new InvalidOperationException($"参数格式必须为 `Type name`：{line}");

                string type = arg.Substring(0, lastSpace).Trim();
                string argName = arg.Substring(lastSpace + 1).Trim();
                args.Add(new ApiArg(type, argName));
            }

            fn = new ApiFn(name, args);
            return true;
        }

        private static List<string> SplitTopLevel(string s)
        {
            var list = new List<string>();
            var cur = new StringBuilder();
            int depth = 0;
            foreach (char c in s)
            {
                if (c == '(') depth++;
                if (c == ')') depth--;

                if (c == ',' && depth == 0)
                {
                    list.Add(cur.ToString());
                    cur.Clear();
                    continue;
                }

                cur.Append(c);
            }
            if (cur.Length > 0)
                list.Add(cur.ToString());
            return list;
        }
    }

    private sealed record ApiFn(string Name, List<ApiArg> Args);
    private sealed record ApiArg(string CppType, string Name);

    private static class CppEmitter
    {
        public static string Emit(ApiModel model, string module, string cppNamespace)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#pragma once");
            sb.AppendLine();
            sb.AppendLine("#include <bridge/bridge.h>");
            sb.AppendLine("#include <bridge/runtime/core_context.h>");
            sb.AppendLine();
            sb.AppendLine("#include <cstdint>");
            sb.AppendLine("#include <string>");
            sb.AppendLine("#include <string_view>");
            sb.AppendLine();
            sb.AppendLine($"namespace {cppNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("\tenum class HostFuncId : uint32_t");
            sb.AppendLine("\t{");
            for (int i = 0; i < model.HostFns.Count; i++)
            {
                uint id = ComputeHostFuncId(module, model.HostFns[i].Name);
                sb.AppendLine($"\t\t{model.HostFns[i].Name} = 0x{id:X8}u,");
            }
            sb.AppendLine("\t};");
            sb.AppendLine();
            sb.AppendLine("\tenum class CoreFuncId : uint32_t");
            sb.AppendLine("\t{");
            for (int i = 0; i < model.CoreFns.Count; i++)
            {
                uint id = ComputeCoreFuncId(module, model.CoreFns[i].Name);
                sb.AppendLine($"\t\t{model.CoreFns[i].Name} = 0x{id:X8}u,");
            }
            sb.AppendLine("\t};");
            sb.AppendLine();

            foreach (var fn in model.HostFns)
            {
                sb.AppendLine($"\tstruct HostArgs_{fn.Name}");
                sb.AppendLine("\t{");
                foreach (var arg in fn.Args)
                    sb.AppendLine($"\t\t{arg.CppType} {ToSnake(arg.Name)};");
                sb.AppendLine("\t};");
                sb.AppendLine();
            }

            foreach (var fn in model.CoreFns)
            {
                sb.AppendLine($"\tstruct CoreArgs_{fn.Name}");
                sb.AppendLine("\t{");
                foreach (var arg in fn.Args)
                    sb.AppendLine($"\t\t{arg.CppType} {ToSnake(arg.Name)};");
                sb.AppendLine("\t};");
                sb.AppendLine();
            }

            sb.AppendLine("\t// Core -> Host 调用（写入 command stream）");
            foreach (var fn in model.HostFns)
            {
                sb.Append($"\tinline void {fn.Name}(bridge::CoreContext& ctx");
                foreach (var arg in fn.Args)
                {
                    sb.Append(", ");
                    sb.Append(MapCppCallArgType(arg.CppType));
                    sb.Append(' ');
                    sb.Append(arg.Name);
                }
                sb.AppendLine(")");
                sb.AppendLine("\t{");
                sb.AppendLine($"\t\tHostArgs_{fn.Name} a{{}};");
                foreach (var arg in fn.Args)
                {
                    if (arg.CppType == "BridgeStringView")
                    {
                        sb.AppendLine($"\t\ta.{ToSnake(arg.Name)} = ctx.StoreUtf8(std::string({arg.Name}));");
                    }
                    else
                    {
                        sb.AppendLine($"\t\ta.{ToSnake(arg.Name)} = {arg.Name};");
                    }
                }
                sb.AppendLine($"\t\tctx.CallHost(static_cast<uint32_t>(HostFuncId::{fn.Name}), &a, static_cast<uint32_t>(sizeof(a)));");
                sb.AppendLine("\t}");
                sb.AppendLine();
            }

            sb.AppendLine($"}} // namespace {cppNamespace}");
            return sb.ToString();
        }

        private static string ToSnake(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static string MapCppCallArgType(string cppType)
        {
            return cppType == "BridgeStringView" ? "std::string_view" : cppType;
        }
    }

    private static class CsEmitter
    {
        public sealed record CsFile(string FileName, string Contents);

        public static List<CsFile> EmitFiles(ApiModel model, string module, string csNamespace)
        {
            var files = new List<CsFile>();
            files.Add(new CsFile($"{module}.Ids.g.cs", EmitIds(model, module, csNamespace)));
            files.Add(new CsFile($"{module}.Structs.g.cs", EmitStructs(model, csNamespace)));
            files.Add(new CsFile($"I{module}HostApi.g.cs", EmitHostApi(model, module, csNamespace)));
            files.Add(new CsFile($"{module}.CoreCalls.g.cs", EmitCoreCalls(model, module, csNamespace)));
            return files;
        }

        public static List<CsFile> EmitAggregateFiles(IReadOnlyList<CsModule> modules)
        {
            var files = new List<CsFile>();
            files.Add(new CsFile("Bridge.AllCommandDispatcher.g.cs", EmitAllDispatcher(modules)));
            return files;
        }

        private static string AutoHeader()
        {
            return string.Join(
                "\n",
                "// <auto-generated>",
                "// 由 Core/Tools/BridgeGen 生成，请勿手改。",
                "// </auto-generated>",
                ""
            );
        }

        private static string EmitIds(ApiModel model, string module, string csNamespace)
        {
            var sb = new StringBuilder();
            sb.AppendLine(AutoHeader());
            sb.AppendLine("namespace " + csNamespace);
            sb.AppendLine("{");
            sb.AppendLine("    public enum HostFuncId : uint");
            sb.AppendLine("    {");
            for (int i = 0; i < model.HostFns.Count; i++)
            {
                uint id = ComputeHostFuncId(module, model.HostFns[i].Name);
                sb.AppendLine($"        {model.HostFns[i].Name} = 0x{id:X8}u,");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public enum CoreFuncId : uint");
            sb.AppendLine("    {");
            for (int i = 0; i < model.CoreFns.Count; i++)
            {
                uint id = ComputeCoreFuncId(module, model.CoreFns[i].Name);
                sb.AppendLine($"        {model.CoreFns[i].Name} = 0x{id:X8}u,");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EmitAllDispatcher(IReadOnlyList<CsModule> modules)
        {
            var sb = new StringBuilder();
            sb.AppendLine(AutoHeader());
            sb.AppendLine("using Bridge.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace Bridge.Bindings");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Host API 基类：用虚函数分发替代接口调用，降低 IL2CPP 下的 dispatch 开销。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public abstract class BridgeAllHostApiBase");
            bool wroteBaseList = false;
            foreach (var m in modules)
            {
                if (m.Model.HostFns.Count == 0)
                    continue;
                sb.Append(wroteBaseList ? ", " : "        : ");
                wroteBaseList = true;
                sb.Append(m.CsNamespace);
                sb.Append(".I");
                sb.Append(m.Module);
                sb.Append("HostApi");
            }
            if (wroteBaseList)
                sb.AppendLine();
            sb.AppendLine("    {");
            foreach (var m in modules)
            {
                if (m.Model.HostFns.Count == 0)
                    continue;

                foreach (var fn in m.Model.HostFns)
                {
                    sb.Append("        public abstract void ");
                    sb.Append(fn.Name);
                    sb.Append('(');
                    for (int i = 0; i < fn.Args.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        var arg = fn.Args[i];
                        sb.Append(MapCsHostArgParamType(arg.CppType));
                        sb.Append(' ');
                        sb.Append(ToCamel(arg.Name));
                    }
                    sb.AppendLine(");");
                }
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 单次扫描 command stream，并按 func_id 分发到各模块 Host API。");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class BridgeAllCommandDispatcher");
            sb.AppendLine("    {");
            sb.AppendLine("        public static unsafe void DispatchFast<THost>(CommandStream stream, THost host)");
            sb.AppendLine("            where THost : BridgeAllHostApiBase");
            sb.AppendLine("        {");
            sb.AppendLine("            if (host == null || stream.Ptr == System.IntPtr.Zero || stream.Length == 0)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            sb.AppendLine("            byte* cursor = (byte*)stream.Ptr;");
            sb.AppendLine("            byte* end = cursor + (int)stream.Length;");
            sb.AppendLine();
            sb.AppendLine("            while (cursor < end)");
            sb.AppendLine("            {");
            sb.AppendLine("                int remaining = (int)(end - cursor);");
            sb.AppendLine("                if (remaining < (int)sizeof(BridgeCommandHeader))");
            sb.AppendLine("                    break;");
            sb.AppendLine();
            sb.AppendLine("                var header = (BridgeCommandHeader*)cursor;");
            sb.AppendLine("                int size = header->Size;");
            sb.AppendLine("                if ((uint)size < (uint)sizeof(BridgeCommandHeader) || (uint)size > (uint)remaining)");
            sb.AppendLine("                    break;");
            sb.AppendLine();
            sb.AppendLine("                if (header->Type == (ushort)BridgeCommandType.CallHost && size >= sizeof(BridgeCmdCallHost))");
            sb.AppendLine("                {");
            sb.AppendLine("                    var cmd = (BridgeCmdCallHost*)cursor;");
            sb.AppendLine("                    uint payloadBytes = (uint)(size - sizeof(BridgeCmdCallHost));");
            sb.AppendLine("                    byte* payloadPtr = cursor + sizeof(BridgeCmdCallHost);");
            sb.AppendLine();
            sb.AppendLine("                    switch (cmd->FuncId)");
            sb.AppendLine("                    {");

            foreach (var m in modules)
            {
                if (m.Model.HostFns.Count == 0)
                    continue;

                foreach (var fn in m.Model.HostFns)
                {
                    uint id = ComputeHostFuncId(m.Module, fn.Name);
                    sb.AppendLine($"                            case 0x{id:X8}u:");
                    sb.AppendLine("                            {");
                    sb.Append("                                if (payloadBytes >= (uint)sizeof(");
                    sb.Append(m.CsNamespace);
                    sb.Append(".HostArgs_");
                    sb.Append(fn.Name);
                    sb.AppendLine("))");
                    sb.AppendLine("                                {");
                    sb.Append("                                    ref readonly ");
                    sb.Append(m.CsNamespace);
                    sb.Append(".HostArgs_");
                    sb.Append(fn.Name);
                    sb.Append(" a = ref *((");
                    sb.Append(m.CsNamespace);
                    sb.Append(".HostArgs_");
                    sb.Append(fn.Name);
                    sb.AppendLine("*)payloadPtr);");
                    sb.Append("                                    host.");
                    sb.Append(fn.Name);
                    sb.Append('(');
                    for (int i = 0; i < fn.Args.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        var arg = fn.Args[i];
                        string field = $"a.{ToPascal(arg.Name)}";
                        sb.Append(MapCsHostArgExpr(arg.CppType, field));
                    }
                    sb.AppendLine(");");
                    sb.AppendLine("                                }");
                    sb.AppendLine("                                break;");
                    sb.AppendLine("                            }");
                }
            }

            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                cursor += size;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public static unsafe void DispatchFastUnchecked<THost>(CommandStream stream, THost host)");
            sb.AppendLine("            where THost : BridgeAllHostApiBase");
            sb.AppendLine("        {");
            sb.AppendLine("            if (host == null || stream.Ptr == System.IntPtr.Zero || stream.Length == 0)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            sb.AppendLine("            byte* cursor = (byte*)stream.Ptr;");
            sb.AppendLine("            byte* end = cursor + (int)stream.Length;");
            sb.AppendLine();
            sb.AppendLine("            while (cursor < end)");
            sb.AppendLine("            {");
            sb.AppendLine("                int remaining = (int)(end - cursor);");
            sb.AppendLine("                if (remaining < (int)sizeof(BridgeCmdCallHost))");
            sb.AppendLine("                    break;");
            sb.AppendLine();
            sb.AppendLine("                var cmd = (BridgeCmdCallHost*)cursor;");
            sb.AppendLine("                int size = cmd->Header.Size;");
            sb.AppendLine("                if ((uint)size < (uint)sizeof(BridgeCmdCallHost) || (uint)size > (uint)remaining)");
            sb.AppendLine("                    break;");
            sb.AppendLine();
            sb.AppendLine("                byte* payloadPtr = cursor + sizeof(BridgeCmdCallHost);");
            sb.AppendLine();
            sb.AppendLine("                switch (cmd->FuncId)");
            sb.AppendLine("                {");

            foreach (var m in modules)
            {
                if (m.Model.HostFns.Count == 0)
                    continue;

                foreach (var fn in m.Model.HostFns)
                {
                    uint id = ComputeHostFuncId(m.Module, fn.Name);
                    sb.AppendLine($"                    case 0x{id:X8}u:");
                    sb.AppendLine("                    {");
                    sb.Append("                        ref readonly ");
                    sb.Append(m.CsNamespace);
                    sb.Append(".HostArgs_");
                    sb.Append(fn.Name);
                    sb.Append(" a = ref *((");
                    sb.Append(m.CsNamespace);
                    sb.Append(".HostArgs_");
                    sb.Append(fn.Name);
                    sb.AppendLine("*)payloadPtr);");
                    sb.Append("                        host.");
                    sb.Append(fn.Name);
                    sb.Append('(');
                    for (int i = 0; i < fn.Args.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        var arg = fn.Args[i];
                        string field = $"a.{ToPascal(arg.Name)}";
                        sb.Append(MapCsHostArgExpr(arg.CppType, field));
                    }
                    sb.AppendLine(");");
                    sb.AppendLine("                        break;");
                    sb.AppendLine("                    }");
                }
            }

            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                cursor += size;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static unsafe void DispatchFastUnchecked(CommandStream stream, BridgeAllHostApiBase host)");
            sb.AppendLine("            => DispatchFastUnchecked<BridgeAllHostApiBase>(stream, host);");
            sb.AppendLine();

            sb.AppendLine("        public static unsafe void DispatchFast(CommandStream stream, BridgeAllHostApiBase host)");
            sb.AppendLine("            => DispatchFast<BridgeAllHostApiBase>(stream, host);");
            sb.AppendLine();
            sb.AppendLine("        public static unsafe void Dispatch<THost>(CommandStream stream, THost host)");
            sb.Append("            where THost : class");

            foreach (var m in modules)
            {
                if (m.Model.HostFns.Count == 0)
                    continue;
                sb.Append(", ");
                sb.Append(m.CsNamespace);
                sb.Append(".I");
                sb.Append(m.Module);
                sb.Append("HostApi");
            }
            sb.AppendLine();
            sb.AppendLine("        {");
            sb.AppendLine("            if (stream.IsEmpty || host == null)");
            sb.AppendLine("                return;");
            sb.AppendLine();
            sb.AppendLine("            byte* cursor = (byte*)stream.Ptr;");
            sb.AppendLine("            byte* end = cursor + (int)stream.Length;");
            sb.AppendLine();
            sb.AppendLine("            while (cursor < end)");
            sb.AppendLine("            {");
            sb.AppendLine("                int remaining = (int)(end - cursor);");
            sb.AppendLine("                if (remaining < (int)sizeof(BridgeCommandHeader))");
            sb.AppendLine("                    break;");
            sb.AppendLine();
            sb.AppendLine("                var header = (BridgeCommandHeader*)cursor;");
            sb.AppendLine("                int size = header->Size;");
            sb.AppendLine("                if ((uint)size < (uint)sizeof(BridgeCommandHeader) || (uint)size > (uint)remaining)");
            sb.AppendLine("                    break;");
            sb.AppendLine();
            sb.AppendLine("                if (header->Type == (ushort)BridgeCommandType.CallHost && size >= sizeof(BridgeCmdCallHost))");
            sb.AppendLine("                {");
            sb.AppendLine("                    var cmd = (BridgeCmdCallHost*)cursor;");
            sb.AppendLine("                    uint payloadBytes = (uint)(size - sizeof(BridgeCmdCallHost));");
            sb.AppendLine("                    byte* payloadPtr = cursor + sizeof(BridgeCmdCallHost);");
            sb.AppendLine();
            sb.AppendLine("                    switch (cmd->FuncId)");
            sb.AppendLine("                    {");

            foreach (var m in modules)
            {
                if (m.Model.HostFns.Count == 0)
                    continue;

                foreach (var fn in m.Model.HostFns)
                {
                    uint id = ComputeHostFuncId(m.Module, fn.Name);
                    sb.AppendLine($"                            case 0x{id:X8}u:");
                    sb.AppendLine("                            {");
                    sb.Append("                                if (payloadBytes >= (uint)sizeof(");
                    sb.Append(m.CsNamespace);
                    sb.Append(".HostArgs_");
                    sb.Append(fn.Name);
                    sb.AppendLine("))");
                    sb.AppendLine("                                {");
                    sb.Append("                                    ref readonly ");
                    sb.Append(m.CsNamespace);
                    sb.Append(".HostArgs_");
                    sb.Append(fn.Name);
                    sb.Append(" a = ref *((");
                    sb.Append(m.CsNamespace);
                    sb.Append(".HostArgs_");
                    sb.Append(fn.Name);
                    sb.AppendLine("*)payloadPtr);");
                    sb.Append("                                    host.");
                    sb.Append(fn.Name);
                    sb.Append('(');
                    for (int i = 0; i < fn.Args.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        var arg = fn.Args[i];
                        string field = $"a.{ToPascal(arg.Name)}";
                        sb.Append(MapCsHostArgExpr(arg.CppType, field));
                    }
                    sb.AppendLine(");");
                    sb.AppendLine("                                }");
                    sb.AppendLine("                                break;");
                    sb.AppendLine("                            }");
                }
            }

            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                cursor += size;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EmitStructs(ApiModel model, string csNamespace)
        {
            var sb = new StringBuilder();
            sb.AppendLine(AutoHeader());
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using Bridge.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace " + csNamespace);
            sb.AppendLine("{");

            foreach (var fn in model.HostFns)
            {
                sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
                sb.AppendLine($"    public struct HostArgs_{fn.Name}");
                sb.AppendLine("    {");
                foreach (var arg in fn.Args)
                    sb.AppendLine($"        public {MapCsInteropType(arg.CppType)} {ToPascal(arg.Name)};");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            foreach (var fn in model.CoreFns)
            {
                sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
                sb.AppendLine($"    public struct CoreArgs_{fn.Name}");
                sb.AppendLine("    {");
                foreach (var arg in fn.Args)
                    sb.AppendLine($"        public {MapCsInteropType(arg.CppType)} {ToPascal(arg.Name)};");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EmitHostApi(ApiModel model, string module, string csNamespace)
        {
            var sb = new StringBuilder();
            sb.AppendLine(AutoHeader());
            sb.AppendLine("using Bridge.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace " + csNamespace);
            sb.AppendLine("{");
            sb.AppendLine($"    public interface I{module}HostApi");
            sb.AppendLine("    {");
            foreach (var fn in model.HostFns)
            {
                sb.Append($"        void {fn.Name}(");
                for (int i = 0; i < fn.Args.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var arg = fn.Args[i];
                    sb.Append(MapCsHostArgParamType(arg.CppType));
                    sb.Append(' ');
                    sb.Append(ToCamel(arg.Name));
                }
                sb.AppendLine(");");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EmitCoreCalls(ApiModel model, string module, string csNamespace)
        {
            var sb = new StringBuilder();
            sb.AppendLine(AutoHeader());
            sb.AppendLine("using Bridge.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace " + csNamespace);
            sb.AppendLine("{");
            sb.AppendLine($"    public static class {module}CoreCalls");
            sb.AppendLine("    {");
            foreach (var fn in model.CoreFns)
            {
                sb.Append($"        public static void {fn.Name}(this BridgeCore core");
                foreach (var arg in fn.Args)
                {
                    sb.Append(", ");
                    sb.Append(MapCsCoreCallArgParamType(arg.CppType));
                    sb.Append(' ');
                    sb.Append(ToCamel(arg.Name));
                }
                sb.AppendLine(")");
                sb.AppendLine("        {");
                sb.AppendLine($"            var a = new CoreArgs_{fn.Name}");
                sb.AppendLine("            {");
                foreach (var arg in fn.Args)
                    sb.AppendLine($"                {ToPascal(arg.Name)} = {ToCamel(arg.Name)},");
                sb.AppendLine("            };");
                sb.AppendLine($"            core.PushCallCore((uint)CoreFuncId.{fn.Name}, a);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string ToPascal(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static string ToCamel(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static string MapCsInteropType(string cppType)
        {
            return cppType switch
            {
                "uint64_t" => "ulong",
                "uint32_t" => "uint",
                "BridgeLogLevel" => "BridgeLogLevel",
                "BridgeAssetType" => "BridgeAssetType",
                "BridgeAssetStatus" => "BridgeAssetStatus",
                "BridgeVec3" => "BridgeVec3",
                "BridgeQuat" => "BridgeQuat",
                "BridgeTransform" => "BridgeTransform",
                "BridgeStringView" => "BridgeStringView",
                _ => throw new InvalidOperationException($"未支持的 C++ 类型：{cppType}")
            };
        }

        private static string MapCsHostArgType(string cppType)
        {
            return cppType switch
            {
                "BridgeStringView" => "BridgeStringView",
                "BridgeLogLevel" => "BridgeLogLevel",
                "BridgeAssetType" => "BridgeAssetType",
                "BridgeAssetStatus" => "BridgeAssetStatus",
                "BridgeTransform" => "BridgeTransform",
                "uint64_t" => "ulong",
                "uint32_t" => "uint",
                _ => MapCsInteropType(cppType),
            };
        }

        private static string MapCsHostArgParamType(string cppType)
        {
            string type = MapCsHostArgType(cppType);
            return cppType switch
            {
                // Hot path：避免 48B+ struct 的值拷贝（尤其是 SetTransform 高频）。
                "BridgeTransform" => "in " + type,
                _ => type,
            };
        }

        private static string MapCsCoreCallArgType(string cppType)
        {
            if (cppType == "BridgeStringView")
                throw new InvalidOperationException("Core API（Host->Core）禁止使用 BridgeStringView；请改为传 handle/hash/id。");
            return MapCsHostArgType(cppType);
        }

        private static string MapCsCoreCallArgParamType(string cppType)
        {
            string type = MapCsCoreCallArgType(cppType);
            return cppType switch
            {
                "BridgeTransform" => "in " + type,
                _ => type,
            };
        }

        private static string MapCsHostArgExpr(string cppType, string fieldExpr)
        {
            return cppType switch
            {
                "BridgeTransform" => "in " + fieldExpr,
                _ => fieldExpr
            };
        }
    }
}
