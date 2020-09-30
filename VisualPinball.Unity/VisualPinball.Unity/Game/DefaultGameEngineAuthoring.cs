using UnityEngine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Logic Engine/Default Game Logic")]
	public class DefaultGameEngineAuthoring : MonoBehaviour
	{
		public DefaultGamelogicEngine GameEngine = new DefaultGamelogicEngine();
	}
}
