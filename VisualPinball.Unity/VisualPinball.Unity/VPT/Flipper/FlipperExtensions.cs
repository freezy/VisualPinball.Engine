using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Flipper
{
	public static class FlipperExtensions
	{
		public static FlipperBehavior SetupGameObject(this Engine.VPT.Flipper.Flipper flipper, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<FlipperBehavior>().SetItem(flipper);
			obj.AddComponent<ConvertToEntity>();
			return ic as FlipperBehavior;
		}
	}
}
