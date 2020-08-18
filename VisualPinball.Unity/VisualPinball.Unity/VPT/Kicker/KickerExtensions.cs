using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class KickerExtensions
	{
		public static KickerAuthoring SetupGameObject(this Engine.VPT.Kicker.Kicker kicker, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<KickerAuthoring>().SetItem(kicker);
			obj.AddComponent<ConvertToEntity>();
			return ic as KickerAuthoring;
		}
	}
}
