using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Rubber
{
	public static class RubberExtensions
	{
		public static RubberBehavior SetupGameObject(this Engine.VPT.Rubber.Rubber rubber, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<RubberBehavior>().SetData(rubber.Data);
			obj.AddComponent<ConvertToEntity>();
			return ic as RubberBehavior;
		}
	}
}
