using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class FlipperExtensions
	{
		public static FlipperAuthoring SetupGameObject(this Engine.VPT.Flipper.Flipper flipper, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<FlipperAuthoring>().SetItem(flipper);
			obj.AddComponent<ConvertToEntity>();
			return ic as FlipperAuthoring;
		}
	}
}
