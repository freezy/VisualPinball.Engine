using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Trigger
{
	public static class TriggerExtensions
	{
		public static TriggerBehavior SetupGameObject(this Engine.VPT.Trigger.Trigger trigger, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<TriggerBehavior>().SetItem(trigger);
			obj.AddComponent<ConvertToEntity>();
			return ic as TriggerBehavior;
		}
	}
}
