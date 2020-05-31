using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Flipper
{
	public static class FlipperExtensions
	{
		public static FlipperBehavior SetupGameObject(this Engine.VPT.Flipper.Flipper flipper, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<FlipperBehavior>().SetData(flipper.Data);
			obj.AddComponent<ConvertToEntity>();
			return ic as FlipperBehavior;
		}
	}
}
