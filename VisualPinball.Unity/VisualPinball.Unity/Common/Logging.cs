using NLog;
using UnityEngine;

namespace VisualPinball.Unity
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
