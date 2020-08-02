using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Kicker
{
	public static class KickerExtensions
	{
		public static KickerBehavior SetupGameObject(this Engine.VPT.Kicker.Kicker kicker, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<KickerBehavior>().SetItem(kicker);
			obj.AddComponent<ConvertToEntity>();
			return ic as KickerBehavior;
		}
	}
}
