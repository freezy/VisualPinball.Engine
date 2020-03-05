using NLog;
using UnityEngine;
using VisualPinball.Unity.IO;

namespace VisualPinball.Unity.Common
{
	public static class Logging
	{
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
