using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bridge.Core.Unity.Editor
{
	/// <summary>
	/// Windows 下辅助同步/配置 bridge_core.dll：
	/// - 可把构建产物同步到 Assets/Plugins（用于 Player 出包）
	/// - 并确保插件不在 Editor 自动加载，避免锁定
	/// </summary>
	public static class BridgeCoreWinSync
	{
		private const string PluginRelativePath = "Assets/Plugins/BridgeCore/Win64/bridge_core.dll";

		[MenuItem("BridgeCore/Windows/Sync bridge_core.dll (for Player)")]
		public static void SyncForPlayer()
		{
			string sourceDll = FindSourceBridgeCoreDll();
			if (string.IsNullOrEmpty(sourceDll) || !File.Exists(sourceDll))
			{
				EditorUtility.DisplayDialog(
					"BridgeCore",
					"未找到源 bridge_core.dll。\n\n可选：设置环境变量 BRIDGE_CORE_DLL 指向 DLL 路径。\n或先在仓库根目录构建 CMake：build/bin/Release/bridge_core.dll",
					"OK");
				return;
			}

			EnsureDir(Path.GetDirectoryName(PluginRelativePath));
			File.Copy(sourceDll, PluginRelativePath, true);
			AssetDatabase.ImportAsset(PluginRelativePath, ImportAssetOptions.ForceUpdate);

			ConfigurePluginImporter();

			Debug.Log("BridgeCore: synced " + sourceDll + " -> " + PluginRelativePath);
		}

		[MenuItem("BridgeCore/Windows/Configure Plugin Importer")]
		public static void ConfigurePluginImporter()
		{
			var importer = AssetImporter.GetAtPath(PluginRelativePath) as PluginImporter;
			if (importer == null)
			{
				EditorUtility.DisplayDialog(
					"BridgeCore",
					"未找到插件资源：" + PluginRelativePath + "\n先执行 Sync bridge_core.dll。",
					"OK");
				return;
			}

			// 避免 Editor 自动加载并锁定文件，Editor 运行时由 BridgeCoreWinLoader 负责从 Library 加载
			importer.SetCompatibleWithAnyPlatform(false);
			importer.SetCompatibleWithEditor(false);
			importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);

#if UNITY_2019_2_OR_NEWER
			importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
#endif

			EditorUtility.SetDirty(importer);
			importer.SaveAndReimport();
		}

		private static string FindSourceBridgeCoreDll()
		{
			string env = Environment.GetEnvironmentVariable("BRIDGE_CORE_DLL");
			if (!string.IsNullOrEmpty(env) && File.Exists(env))
				return env;

			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			string repoRoot = FindRepoRoot(projectRoot);
			string[] candidates = new[]
			{
				Path.Combine(repoRoot, "build", "bin", "Release", "bridge_core.dll"),
				Path.Combine(repoRoot, "build", "bin", "Debug", "bridge_core.dll"),
			};
			for (int i = 0; i < candidates.Length; i++)
			{
				if (File.Exists(candidates[i]))
					return candidates[i];
			}
			return string.Empty;
		}

		private static string FindRepoRoot(string startDir)
		{
			string dir = startDir;
			for (int i = 0; i < 10; i++)
			{
				if (File.Exists(Path.Combine(dir, "CMakeLists.txt")) &&
				    Directory.Exists(Path.Combine(dir, "Core")))
				{
					return dir;
				}

				var parent = Directory.GetParent(dir);
				if (parent == null)
					break;
				dir = parent.FullName;
			}
			return startDir;
		}

		private static void EnsureDir(string dir)
		{
			if (string.IsNullOrEmpty(dir))
				return;
			if (Directory.Exists(dir))
				return;
			Directory.CreateDirectory(dir);
		}
	}
}
