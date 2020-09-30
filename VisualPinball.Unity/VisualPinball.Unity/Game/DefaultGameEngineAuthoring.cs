using UnityEngine;
using VisualPinball.Engine.Game.Engine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Logic Engine/Default Game Logic")]
	public class DefaultGameEngineAuthoring : MonoBehaviour
	{
		public DefaultGamelogicEngine GameEngine = new DefaultGamelogicEngine();
	}
}
