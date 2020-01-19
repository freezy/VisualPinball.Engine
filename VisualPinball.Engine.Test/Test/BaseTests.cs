using NLog;
using NLog.Targets;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.Test
{
	public abstract class BaseTests
	{
		protected readonly Logger Logger;

		protected BaseTests(ITestOutputHelper output)
		{
			var config = new NLog.Config.LoggingConfiguration();
			var logConsole = new TestTarget(output);
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);
			LogManager.Configuration = config;
			Logger = LogManager.GetCurrentClassLogger();
		}
	}

	[Target("Test")]
	public class TestTarget : TargetWithLayout
	{
		private readonly ITestOutputHelper _output;

		public TestTarget(ITestOutputHelper output)
		{
			_output = output;
		}

		protected override void Write(LogEventInfo logEvent)
		{
			var msg = Layout.Render(logEvent);
			_output.WriteLine(msg);
		}
	}
}
