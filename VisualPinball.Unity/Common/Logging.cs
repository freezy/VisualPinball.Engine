using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.IO;

namespace VisualPinball.Unity.Common
{
	[InitializeOnLoad]
	public static class Logging
	{
		static Logging()
		{
			Setup();
		}

		[RuntimeInitializeOnLoadMethod]
		public static void Setup()
		{
			var config = new NLog.Config.LoggingConfiguration();
			var logConsole = new UnityTarget();
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);
			LogManager.Configuration = config;
		}
	}
}
