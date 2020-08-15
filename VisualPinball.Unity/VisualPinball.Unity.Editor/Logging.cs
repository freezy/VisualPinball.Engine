using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[InitializeOnLoad]
	public static class Logging
	{
		static Logging()
		{
			Unity.Logging.Setup();
		}
	}
}
