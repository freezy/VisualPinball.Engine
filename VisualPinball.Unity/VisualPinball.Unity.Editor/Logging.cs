using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[InitializeOnLoad]
	public static class Logging
	{
		static Logging()
		{
			Common.Logging.Setup();
		}
	}
}
