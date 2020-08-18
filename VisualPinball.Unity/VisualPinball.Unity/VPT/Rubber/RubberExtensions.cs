using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class RubberExtensions
	{
		public static RubberAuthoring SetupGameObject(this Engine.VPT.Rubber.Rubber rubber, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<RubberAuthoring>().SetItem(rubber);
			obj.AddComponent<ConvertToEntity>();
			return ic as RubberAuthoring;
		}
	}
}
