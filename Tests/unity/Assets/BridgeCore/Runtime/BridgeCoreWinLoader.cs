using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Bridge.Core.Unity
{
	/// <summary>
	/// Windows 下 Unity Editor 的原生 DLL “先拷贝再加载”：
	/// - 避免锁定固定源文件（便于热替换/重编译覆盖）
	/// - 运行时从 Library 临时目录加载
	/// </summary>
	public static class BridgeCoreWinLoader
	{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool SetDllDirectory(string lpPathName);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern IntPtr LoadLibrary(string lpFileName);
#endif

		private static bool s_loaded;
		private static string s_loadedDir;
		private static string s_loadedDllPath;
		private static IntPtr s_moduleHandle;

#if UNITY_EDITOR_WIN
		[UnityEditor.InitializeOnLoadMethod]
		private static void EditorInit()
		{
			// 只做一次尝试：避免不停弹错误；真正需要时也可手动调用 EnsureLoaded()
			TryEnsureLoaded();
		}
#endif

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void RuntimeInit()
		{
			TryEnsureLoaded();
		}

		public static void TryEnsureLoaded()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			TryLoadLatest(force: false);
#endif
		}

		public static void TryReloadLatest()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			TryLoadLatest(force: true);
#endif
		}

		public static IntPtr GetLoadedModuleHandle()
		{
			return s_moduleHandle;
		}

		public static string GetLoadedDllPath()
		{
			return s_loadedDllPath ?? string.Empty;
		}

		private static void TryLoadLatest(bool force)
		{
			if (s_loaded)
			{
				if (!force)
					return;
			}

#if !(UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
			return;
#else
			string sourceDll = FindSourceBridgeCoreDll();
			if (string.IsNullOrEmpty(sourceDll) || !File.Exists(sourceDll))
				return;

			string destDir = PrepareTempDir(sourceDll);
			if (string.IsNullOrEmpty(destDir))
				return;

			if (!force &&
			    s_loaded &&
			    !string.IsNullOrEmpty(s_loadedDir) &&
			    string.Equals(s_loadedDir, destDir, StringComparison.OrdinalIgnoreCase) &&
			    s_moduleHandle != IntPtr.Zero)
			{
				return;
			}

			CopyAllDlls(Path.GetDirectoryName(sourceDll), destDir);

			// 让后续 DllImport("bridge_core") 能在此目录找到 dll
			SetDllDirectory(destDir);

			string destDll = Path.Combine(destDir, "bridge_core.dll");
			IntPtr h = LoadLibrary(destDll);
			if (h == IntPtr.Zero)
			{
				// 加载失败时不要抛异常，避免卡住编辑器；需要时可在 Console 看到错误
				int err = Marshal.GetLastWin32Error();
				Debug.LogWarning("BridgeCoreWinLoader: LoadLibrary failed. path=" + destDll + " err=" + err);
				return;
			}

			s_loaded = true;
			s_loadedDir = destDir;
			s_loadedDllPath = destDll;
			s_moduleHandle = h;
#endif
		}

		public static string GetLoadedDirectory()
		{
			return s_loadedDir ?? string.Empty;
		}

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		private static string FindSourceBridgeCoreDll()
		{
			// 优先读环境变量，方便 CI/本机自定义
			string env = Environment.GetEnvironmentVariable("BRIDGE_CORE_DLL");
			if (!string.IsNullOrEmpty(env) && File.Exists(env))
				return env;

			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			string repoRoot = FindRepoRoot(projectRoot);

			string[] candidates = new[]
			{
				Path.Combine(repoRoot, "build", "bin", "Release", "bridge_core.dll"),
				Path.Combine(repoRoot, "build", "bin", "Debug", "bridge_core.dll"),
				Path.Combine(repoRoot, "build", "bin", "bridge_core.dll"),
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

		private static string PrepareTempDir(string sourceDll)
		{
			try
			{
				string projectRoot = Directory.GetParent(Application.dataPath).FullName;
				string libRoot = Path.Combine(projectRoot, "Library", "BridgeNative");

				var fi = new FileInfo(sourceDll);
				string buildId = fi.LastWriteTimeUtc.Ticks.ToString() + "_" + fi.Length.ToString();
				string dir = Path.Combine(libRoot, buildId);
				Directory.CreateDirectory(dir);
				return dir;
			}
			catch (Exception e)
			{
				Debug.LogWarning("BridgeCoreWinLoader: PrepareTempDir failed: " + e);
				return string.Empty;
			}
		}

		private static void CopyAllDlls(string sourceDir, string destDir)
		{
			if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
				return;

			try
			{
				string[] dlls = Directory.GetFiles(sourceDir, "*.dll", SearchOption.TopDirectoryOnly);
				for (int i = 0; i < dlls.Length; i++)
				{
					string src = dlls[i];
					string name = Path.GetFileName(src);
					string dst = Path.Combine(destDir, name);

					// 只在目标不存在或源更新时拷贝
					if (!File.Exists(dst) || File.GetLastWriteTimeUtc(src) != File.GetLastWriteTimeUtc(dst))
						File.Copy(src, dst, true);
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning("BridgeCoreWinLoader: CopyAllDlls failed: " + e);
			}
		}
#endif
	}
}
