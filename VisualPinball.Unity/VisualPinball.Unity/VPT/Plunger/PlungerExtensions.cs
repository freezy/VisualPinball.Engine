using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity.VPT.Plunger
{
	public static class PlungerExtensions
	{
		public static PlungerBehavior SetupGameObject(this Engine.VPT.Plunger.Plunger plunger, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<PlungerBehavior>().SetData(plunger.Data);

			var rod = obj.transform.Find(PlungerMeshGenerator.RodName);
			var spring = obj.transform.Find(PlungerMeshGenerator.SpringName);

			if (rod != null) {
				rod.gameObject.AddComponent<PlungerRodBehavior>();
			}

			if (spring != null) {
				spring.gameObject.AddComponent<PlungerSpringBehavior>();
			}

			obj.AddComponent<ConvertToEntity>();
			return ic as PlungerBehavior;
		}
	}
}
