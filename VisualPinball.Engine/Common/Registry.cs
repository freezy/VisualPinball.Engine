namespace VisualPinball.Engine.Common
{
	public class Registry
	{
		private static Registry _instance;

		public static Registry Instance => _instance ?? (_instance = new Registry());

		public T Get<T>(string key, string value, T fallback) {
			return fallback;
		}
	}
}
