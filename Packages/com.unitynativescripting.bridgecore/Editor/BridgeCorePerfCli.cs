using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Unity.PerformanceTesting.Data;

namespace Bridge.Core.Unity.Editor
{
	public static class BridgeCorePerfCli
	{
		// 命令行示例：
		// Unity.exe -batchmode -nographics -projectPath <proj> -executeMethod Bridge.Core.Unity.Editor.BridgeCorePerfCli.RunEditMode -testResults <xml> -perfTestResults <json> -logFile <log>
		public static void RunEditMode()
		{
			string[] args = Environment.GetCommandLineArgs();
			string xmlPath = FindOption(args, "testResults") ?? Path.Combine(Directory.GetCurrentDirectory(), "TestResults.xml");
			string perfPath = FindOption(args, "perfTestResults") ?? Path.Combine(Directory.GetCurrentDirectory(), "PerfResults.json");

			var callbacks = ScriptableObject.CreateInstance<ResultsAndPerfSavingCallbacks>();
			callbacks.SetPaths(xmlPath, perfPath);

			var api = ScriptableObject.CreateInstance<TestRunnerApi>();
			api.RegisterCallbacks(callbacks);

			var filter = new Filter
			{
				testMode = TestMode.EditMode,
				assemblyNames = new[] { "BridgeDemoGame.PerformanceTests" },
			};

			var settings = new ExecutionSettings(filter)
			{
				runSynchronously = false
			};

			Debug.Log("BridgeCorePerfCli: running EditMode performance tests...");
			api.Execute(settings);
		}

		private static string FindOption(string[] args, string name)
		{
			string dash = "-" + name;
			for (int i = 0; i < args.Length - 1; i++)
			{
				if (string.Equals(args[i], dash, StringComparison.OrdinalIgnoreCase))
					return args[i + 1];
			}
			return null;
		}

		private sealed class ResultsAndPerfSavingCallbacks : ScriptableObject, ICallbacks
		{
			private string _xmlPath = string.Empty;
			private string _perfPath = string.Empty;

			public int ExitCode { get; private set; } = 1;

			public void SetPaths(string xmlPath, string perfPath)
			{
				_xmlPath = xmlPath ?? string.Empty;
				_perfPath = perfPath ?? string.Empty;
			}

			public void RunStarted(ITestAdaptor testsToRun)
			{
			}

			public void RunFinished(ITestResultAdaptor result)
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(_xmlPath))
					{
						EnsureDir(_xmlPath);
						TestRunnerApi.SaveResultToFile(result, _xmlPath);
						Debug.Log("BridgeCorePerfCli: saved test results -> " + _xmlPath);
					}
				}
				catch (Exception e)
				{
					Debug.LogError("BridgeCorePerfCli: failed to save test results:\n" + e);
				}

				try
				{
					if (!string.IsNullOrWhiteSpace(_perfPath))
					{
						var run = ExtractPerformanceRunData(GetTestOutputsRecursively(result));
						if (run != null)
						{
							string json = JsonUtility.ToJson(run, true);
							EnsureDir(_perfPath);
							File.WriteAllText(_perfPath, json);
							Debug.Log("BridgeCorePerfCli: saved perf results -> " + _perfPath);
						}
						else
						{
							Debug.LogWarning("BridgeCorePerfCli: no perf markers found in test output (perfResults.json not written).");
						}
					}
				}
				catch (Exception e)
				{
					Debug.LogError("BridgeCorePerfCli: failed to save perf results:\n" + e);
				}

				ExitCode = (result.FailCount > 0 || result.InconclusiveCount > 0) ? 1 : 0;
				EditorApplication.Exit(ExitCode);
			}

			public void TestStarted(ITestAdaptor test)
			{
			}

			public void TestFinished(ITestResultAdaptor result)
			{
			}

			private static void EnsureDir(string filePath)
			{
				string dir = Path.GetDirectoryName(filePath);
				if (string.IsNullOrWhiteSpace(dir))
					return;
				Directory.CreateDirectory(dir);
			}

			private static string[] GetTestOutputsRecursively(ITestResultAdaptor testResults)
			{
				var outputs = new List<string>(256);
				AccumulateTestRunOutputRecursively(testResults, outputs);
				return outputs.ToArray();
			}

			private static void AccumulateTestRunOutputRecursively(ITestResultAdaptor parent, List<string> outputs)
			{
				foreach (var child in parent.Children)
					AccumulateTestRunOutputRecursively(child, outputs);

				string output = parent.Output;
				if (!string.IsNullOrEmpty(output))
					outputs.Add(output);
			}

			private static Run ExtractPerformanceRunData(string[] testOutputs)
			{
				if (testOutputs == null || testOutputs.Length == 0)
					return null;

				Run run = ExtractPerformanceTestRunInfo(testOutputs);
				if (run == null)
					return null;

				DeserializeTestResults(testOutputs, run);
				return run;
			}

			private static Run ExtractPerformanceTestRunInfo(string[] testOutputs)
			{
				foreach (string output in testOutputs)
				{
					const string pattern = @"##performancetestruninfo2:(.+)\n";
					var matches = Regex.Match(output, pattern);
					if (!matches.Success || matches.Groups.Count < 2)
						continue;

					string json = matches.Groups[1].Value;
					if (string.IsNullOrEmpty(json))
						return null;

					return JsonUtility.FromJson<Run>(json);
				}
				return null;
			}

			private static void DeserializeTestResults(string[] testOutputs, Run run)
			{
				foreach (string output in testOutputs)
				{
					foreach (string line in output.Split('\n'))
					{
						string json = GetJsonFromHashtag("performancetestresult2", line);
						if (json == null)
							continue;

						var result = JsonUtility.FromJson<PerformanceTestResult>(json);
						if (result != null)
							run.Results.Add(result);
					}
				}
			}

			private static string GetJsonFromHashtag(string tag, string line)
			{
				string prefix = "##" + tag + ":";
				if (!line.Contains(prefix))
					return null;

				int jsonStart = line.IndexOf('{');
				if (jsonStart < 0)
					return null;

				int open = 0;
				int i = jsonStart;
				while (i < line.Length && (open > 0 || i == jsonStart))
				{
					char c = line[i];
					switch (c)
					{
						case '{': open++; break;
						case '}': open--; break;
					}
					i++;
				}

				if (open != 0)
					return null;

				return line.Substring(jsonStart, i - jsonStart);
			}
		}
	}
}
