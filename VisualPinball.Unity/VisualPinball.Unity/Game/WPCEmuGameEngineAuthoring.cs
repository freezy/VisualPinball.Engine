using UnityEngine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Logic Engine/WPCEmu Game Logic Engine")]
	public class WPCEmuGameEngineAuthoring : MonoBehaviour, IGameEngineAuthoring
	{
		public string Name;
		public Texture2D Texture;
		public IGamelogicEngine GameEngine => new WPCEmuGamelogicEngine();

		private void Awake()
		{
			Texture = new Texture2D(128, 32);
		}
	}
}
