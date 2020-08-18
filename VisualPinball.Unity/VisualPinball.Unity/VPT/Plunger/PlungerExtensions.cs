using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public static class PlungerExtensions
	{
		public static PlungerAuthoring SetupGameObject(this Plunger plunger, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<PlungerAuthoring>().SetItem(plunger);

			var rod = obj.transform.Find(PlungerMeshGenerator.RodName);
			if (rod != null) {
				rod.gameObject.AddComponent<PlungerRodAuthoring>();
			}

			var spring = obj.transform.Find(PlungerMeshGenerator.SpringName);
			if (spring != null) {
				spring.gameObject.AddComponent<PlungerSpringAuthoring>();
			}

			var flat = obj.transform.Find(PlungerMeshGenerator.FlatName);
			if (flat != null) {
				flat.gameObject.AddComponent<PlungerFlatAuthoring>();
			}

			obj.AddComponent<ConvertToEntity>();
			return ic as PlungerAuthoring;
		}
	}
}
