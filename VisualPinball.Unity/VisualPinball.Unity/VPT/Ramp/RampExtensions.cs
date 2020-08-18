using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class RampExtensions
	{
		public static RampAuthoring SetupGameObject(this Engine.VPT.Ramp.Ramp ramp, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<RampAuthoring>().SetItem(ramp);
			obj.AddComponent<ConvertToEntity>();
			return ic as RampAuthoring;
		}
	}
}
