using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class LightExtensions
	{
		public static LightAuthoring SetupGameObject(this Engine.VPT.Light.Light light, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<LightAuthoring>().SetItem(light);
			return ic as LightAuthoring;
		}
	}
}
