using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class TriggerExtensions
	{
		public static TriggerAuthoring SetupGameObject(this Engine.VPT.Trigger.Trigger trigger, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<TriggerAuthoring>().SetItem(trigger);
			obj.AddComponent<ConvertToEntity>();
			return ic as TriggerAuthoring;
		}
	}
}
