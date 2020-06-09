using NLog;
using NLog.Config;
using NLog.Targets;

namespace VisualPinball.Engine.Common
{
	public static class Logging
	{
		public static void Setup()
		{
			var config = new LoggingConfiguration();
			var logConsole = new ConsoleTarget("console");
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);
			LogManager.Configuration = config;
		}
	}
}
