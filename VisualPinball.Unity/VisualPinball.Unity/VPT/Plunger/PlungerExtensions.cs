using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Plunger
{
	public static class PlungerExtensions
	{
		public static PlungerBehavior SetupGameObject(this Engine.VPT.Plunger.Plunger plunger, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<PlungerBehavior>().SetData(plunger.Data);
			obj.AddComponent<ConvertToEntity>();
			return ic as PlungerBehavior;
		}
	}
}
