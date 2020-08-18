using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class HitTargetExtensions
	{
		public static HitTargetAuthoring SetupGameObject(this Engine.VPT.HitTarget.HitTarget hitTarget, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<HitTargetAuthoring>().SetItem(hitTarget);
			obj.AddComponent<ConvertToEntity>();
			return ic as HitTargetAuthoring;
		}
	}
}
