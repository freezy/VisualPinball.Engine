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
			var ic = obj.AddComponent<PlungerBehavior>().SetItem(plunger);

			var rod = obj.transform.Find(PlungerMeshGenerator.RodName);
			if (rod != null) {
				rod.gameObject.AddComponent<PlungerRodBehavior>();
			}

			var spring = obj.transform.Find(PlungerMeshGenerator.SpringName);
			if (spring != null) {
				spring.gameObject.AddComponent<PlungerSpringBehavior>();
			}

			var flat = obj.transform.Find(PlungerMeshGenerator.FlatName);
			if (flat != null) {
				flat.gameObject.AddComponent<PlungerFlatBehavior>();
			}

			obj.AddComponent<ConvertToEntity>();
			return ic as PlungerBehavior;
		}
	}
}
