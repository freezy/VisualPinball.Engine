using NLog;

namespace VisualPinball.Engine.IO
{
	public static class Logging
	{
		public static void Setup()
		{
			var config = new NLog.Config.LoggingConfiguration();
			var logConsole = new NLog.Targets.ConsoleTarget("console");
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);
			LogManager.Configuration = config;
		}
	}
}
