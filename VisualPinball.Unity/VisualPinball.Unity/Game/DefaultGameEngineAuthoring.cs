using UnityEngine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Logic Engine/Default Game Logic")]
	public class DefaultGameEngineAuthoring : MonoBehaviour, IGameEngineAuthoring
	{
		public IGamelogicEngine GameEngine => new DefaultGamelogicEngine();
	}
}
