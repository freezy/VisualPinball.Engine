using NLog;
using NLog.Layouts;
using NLog.Targets;
using UnityEngine;
using ILogger = UnityEngine.ILogger;

namespace VisualPinball.Unity
{
	[Target("Unity")]
	public class UnityTarget : TargetWithLayout
	{
		private static readonly ILogger Logger = Debug.unityLogger;
		private const string Tag = "VisualPinball";

		public UnityTarget()
		{
			Layout = (Layout) "${logger}|${message}";
		}

		protected override void Write(LogEventInfo logEvent)
		{
			var msg = Layout.Render(logEvent);
			if (logEvent.Level.Ordinal >= 4) {
				Logger.LogError(Tag, msg);

			} else if (logEvent.Level.Ordinal >= 3) {
				Logger.LogWarning(Tag, msg);

			} else {
				Logger.Log(Tag, msg);
			}

			if (logEvent.Exception != null) {
				Logger.LogException(logEvent.Exception);
			}
		}
	}
}
