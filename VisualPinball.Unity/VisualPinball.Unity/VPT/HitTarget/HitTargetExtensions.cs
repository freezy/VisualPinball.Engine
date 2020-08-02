using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.HitTarget
{
	public static class HitTargetExtensions
	{
		public static HitTargetBehavior SetupGameObject(this Engine.VPT.HitTarget.HitTarget hitTarget, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<HitTargetBehavior>().SetItem(hitTarget);
			obj.AddComponent<ConvertToEntity>();
			return ic as HitTargetBehavior;
		}
	}
}
