using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Ramp
{
	public static class RubberExtensions
	{
		public static RampBehavior SetupGameObject(this Engine.VPT.Ramp.Ramp ramp, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<RampBehavior>().SetData(ramp.Data);
			obj.AddComponent<ConvertToEntity>();
			return ic as RampBehavior;
		}
	}
}
