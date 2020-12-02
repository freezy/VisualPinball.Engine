using System;
using System.Linq;
using NLog;

namespace VisualPinball.Unity
{
	public interface IRenderPipeline
	{
		string Name { get; }

		IMaterialConverter MaterialConverter { get; }
	}

	public static class RenderPipeline
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		public static IRenderPipeline Current {
			get {
				if (_current == null) {
					var t = typeof(IRenderPipeline);
					var pipelines = AppDomain.CurrentDomain.GetAssemblies()
						.Where(x => x.FullName.StartsWith("VisualPinball."))
						.SelectMany(x => x.GetTypes())
						.Where(x => x.IsClass && t.IsAssignableFrom(x))
						.Select(x => (IRenderPipeline) Activator.CreateInstance(x))
						.ToArray();

					if (!pipelines.Any()) {
						Logger.Error("No render pipelines found.");
						return null;
					}

					_current = pipelines.First();
				}
				return _current;
			}
		}

		private static IRenderPipeline _current;
	}
}
