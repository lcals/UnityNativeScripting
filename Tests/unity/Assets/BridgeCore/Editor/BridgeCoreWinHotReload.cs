using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bridge.Core.Unity.Editor
{
	public static class BridgeCoreWinHotReload
	{
		private enum PendingAction
		{
			None = 0,
			BuildReleaseAndReload = 1,
		}

		private static PendingAction s_pending;

		[MenuItem("BridgeCore/Windows/Build bridge_core.dll (Release)")]
		public static void BuildRelease()
		{
			if (!EnsureWindows())
				return;

			string repoRoot = FindRepoRoot();
			if (string.IsNullOrEmpty(repoRoot))
				return;

			RunBuildRelease(repoRoot, reloadAfterBuild: false);
		}

		[MenuItem("BridgeCore/Windows/Build + Hot Reload (Release)")]
		public static void BuildReleaseAndReload()
		{
			if (!EnsureWindows())
				return;

			if (EditorApplication.isPlaying)
			{
				bool ok = EditorUtility.DisplayDialog(
					"BridgeCore",
					"热更需要先退出 PlayMode（确保没有存活的 BridgeCore 实例）。\n\n是否退出 PlayMode 后继续 Build + Reload？",
					"退出并继续",
					"取消");
				if (!ok)
					return;

				s_pending = PendingAction.BuildReleaseAndReload;
				EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
				EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
				EditorApplication.isPlaying = false;
				return;
			}

			string repoRoot = FindRepoRoot();
			if (string.IsNullOrEmpty(repoRoot))
				return;

			RunBuildRelease(repoRoot, reloadAfterBuild: true);
		}

		[MenuItem("BridgeCore/Windows/Reload bridge_core.dll (from build output)")]
		public static void ReloadOnly()
		{
			if (!EnsureWindows())
				return;

			if (EditorApplication.isPlaying)
			{
				EditorUtility.DisplayDialog(
					"BridgeCore",
					"请先退出 PlayMode 再 Reload（避免旧 DLL 创建的对象被新 DLL 销毁导致崩溃）。",
					"OK");
				return;
			}

			BridgeCoreWinLoader.TryReloadLatest();
			Debug.Log("BridgeCore: reloaded from " + BridgeCoreWinLoader.GetLoadedDllPath());
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state != PlayModeStateChange.EnteredEditMode)
				return;

			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

			var action = s_pending;
			s_pending = PendingAction.None;

			if (action == PendingAction.BuildReleaseAndReload)
			{
				string repoRoot = FindRepoRoot();
				if (!string.IsNullOrEmpty(repoRoot))
					RunBuildRelease(repoRoot, reloadAfterBuild: true);
			}
		}

		private static bool EnsureWindows()
		{
#if !UNITY_EDITOR_WIN
			EditorUtility.DisplayDialog("BridgeCore", "此菜单仅用于 Windows Editor。", "OK");
			return false;
#else
			return true;
#endif
		}

		private static void RunBuildRelease(string repoRoot, bool reloadAfterBuild)
		{
			try
			{
				EditorUtility.DisplayProgressBar("BridgeCore", "Configuring (cmake)...", 0.1f);
				if (!RunProcess("cmake", $"-S \"{repoRoot}\" -B \"{Path.Combine(repoRoot, "build")}\" -DCMAKE_BUILD_TYPE=Release", repoRoot))
				{
					EditorUtility.DisplayDialog("BridgeCore", "cmake configure 失败。请查看 Console 输出。", "OK");
					return;
				}

				EditorUtility.DisplayProgressBar("BridgeCore", "Building (cmake --build)...", 0.6f);
				if (!RunProcess("cmake", $"--build \"{Path.Combine(repoRoot, "build")}\" --config Release", repoRoot))
				{
					EditorUtility.DisplayDialog("BridgeCore", "cmake build 失败。请查看 Console 输出。", "OK");
					return;
				}

				if (reloadAfterBuild)
				{
					EditorUtility.DisplayProgressBar("BridgeCore", "Reloading bridge_core.dll...", 0.9f);
					BridgeCoreWinLoader.TryReloadLatest();
					Debug.Log("BridgeCore: reloaded from " + BridgeCoreWinLoader.GetLoadedDllPath());
				}

				EditorUtility.DisplayDialog("BridgeCore", reloadAfterBuild ? "Build + Reload 完成。" : "Build 完成。", "OK");
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private static bool RunProcess(string fileName, string arguments, string workingDirectory)
		{
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = fileName,
					Arguments = arguments,
					WorkingDirectory = workingDirectory,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				};

				using (var p = new Process { StartInfo = psi })
				{
					var lines = new List<string>(256);
					p.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) lines.Add(e.Data); };
					p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) lines.Add(e.Data); };

					p.Start();
					p.BeginOutputReadLine();
					p.BeginErrorReadLine();
					p.WaitForExit();

					for (int i = 0; i < lines.Count; i++)
						Debug.Log(lines[i]);

					return p.ExitCode == 0;
				}
			}
			catch (Exception e)
			{
				Debug.LogError("BridgeCore: failed to run process: " + fileName + " " + arguments + "\n" + e);
				return false;
			}
		}

		private static string FindRepoRoot()
		{
			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			string dir = projectRoot;
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

			EditorUtility.DisplayDialog(
				"BridgeCore",
				"无法定位仓库根目录（未找到 CMakeLists.txt 与 Core/）。\n当前工程目录：" + projectRoot,
				"OK");
			return string.Empty;
		}
	}
}
