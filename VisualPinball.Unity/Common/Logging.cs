using NLog;

namespace VisualPinball.Unity.IO
{
	public static class Logging
	{
		public static void Setup()
		{
			var config = new NLog.Config.LoggingConfiguration();
			var logConsole = new UnityTarget();
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);
			LogManager.Configuration = config;
		}
	}
}
