using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bridge.Core.Unity.Editor
{
	public static class BridgeCoreIl2cppBuild
	{
		// 命令行示例：
		// Unity.exe -batchmode -quit -projectPath <proj> -executeMethod Bridge.Core.Unity.Editor.BridgeCoreIl2cppBuild.BuildWindows64 -logFile <log>
		public static void BuildWindows64()
		{
			if (!Application.isBatchMode)
				Debug.Log("BridgeCore: BuildWindows64 invoked (non-batchmode).");

			BridgeCoreNativeSourceSync.SyncSourcesForIl2Cpp();

			EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
			PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);

			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			string outputRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "..", "build", "unity_il2cpp"));
			Directory.CreateDirectory(outputRoot);

			string locationPathName = Path.Combine(outputRoot, "BridgeDemoGame.exe");

			var options = new BuildPlayerOptions
			{
				scenes = new[] { "Assets/BridgeDemoGame/Test.unity" },
				locationPathName = locationPathName,
				target = BuildTarget.StandaloneWindows64,
				options = BuildOptions.None,
			};

			Debug.Log("BridgeCore: building IL2CPP player -> " + locationPathName);
			BuildReport report = BuildPipeline.BuildPlayer(options);
			if (report.summary.result != BuildResult.Succeeded)
			{
				throw new Exception("BridgeCore: IL2CPP build failed: " + report.summary.result);
			}
		}
	}
}

