using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Bridge.Core.Unity.Editor
{
	public static class BridgeCoreRuntimeUnitTestBuild
	{
		// 命令行示例：
		// Unity.exe -quit -batchmode -nographics -logFile -projectPath <proj> -executeMethod Bridge.Core.Unity.Editor.BridgeCoreRuntimeUnitTestBuild.BuildUnitTest
		//   /headless /ScriptBackend IL2CPP /BuildTarget StandaloneWindows64
		public static void BuildUnitTest()
		{
			// 按构建后端选择模式：
			// - IL2CPP：走“源码插件”模式（C++ 编进 GameAssembly.dll）
			// - Mono2x：走“native dll 插件”模式（复制到 Assets/Plugins 供 Player 加载）
			string backend = FindSlashOption(Environment.GetCommandLineArgs(), "ScriptBackend");
			if (string.Equals(backend, "IL2CPP", StringComparison.OrdinalIgnoreCase))
				BridgeCoreNativeSourceSync.SyncSourcesForIl2Cpp();
			else
				BridgeCoreWinSync.SyncForPlayer();

#if UNITY_2021_2_OR_NEWER
			// RuntimeUnitTestToolkit 的 /Headless 会走 Dedicated Server 子目标。
			// 这里强制回到普通 Player，避免环境里残留 Server 子目标导致构建失败。
			EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
#endif

			InvokeUnitTestBuilder();
		}

		private static string FindSlashOption(string[] args, string name)
		{
			string key = "/" + name;
			for (int i = 0; i < args.Length - 1; i++)
			{
				if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
					return args[i + 1];
			}
			return null;
		}

		private static void InvokeUnitTestBuilder()
		{
			Type builderType = AppDomain.CurrentDomain
				.GetAssemblies()
				.Select(a =>
				{
					try { return a.GetType("UnitTestBuilder", throwOnError: false); }
					catch { return null; }
				})
				.FirstOrDefault(t => t != null);

			if (builderType == null)
				throw new InvalidOperationException("UnitTestBuilder not found. Ensure RuntimeUnitTestToolkit is installed in this Unity project.");

			MethodInfo[] candidates = builderType
				.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
				.Where(m => string.Equals(m.Name, "BuildUnitTest", StringComparison.Ordinal))
				.ToArray();

			if (candidates.Length == 0)
				throw new MissingMethodException("UnitTestBuilder", "BuildUnitTest");

			MethodInfo mi = candidates.FirstOrDefault(m => m.GetParameters().Length == 0);
			object[] invokeArgs = null;

			if (mi == null)
			{
				mi = candidates.FirstOrDefault(m =>
				{
					var ps = m.GetParameters();
					return ps.Length == 1 && ps[0].ParameterType == typeof(string[]);
				});
				if (mi != null)
					invokeArgs = new object[] { Environment.GetCommandLineArgs() };
			}

			if (mi == null)
			{
				mi = candidates[0];
				var ps = mi.GetParameters();
				invokeArgs = ps.Length == 0 ? null : new object[ps.Length];
			}

			Debug.Log("BridgeCoreRuntimeUnitTestBuild: invoking UnitTestBuilder.BuildUnitTest ...");
			mi.Invoke(null, invokeArgs);
		}
	}
}
