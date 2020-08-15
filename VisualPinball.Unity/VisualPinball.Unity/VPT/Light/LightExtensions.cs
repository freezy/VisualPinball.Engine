using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class LightExtensions
	{
		public static LightBehavior SetupGameObject(this Engine.VPT.Light.Light light, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<LightBehavior>().SetItem(light);
			return ic as LightBehavior;
		}
	}
}
