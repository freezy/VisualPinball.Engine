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

			var rod = obj.transform.Find("Rod").gameObject;
			var spring = obj.transform.Find("Spring").gameObject;

			rod.AddComponent<PlungerRodBehavior>();
			spring.AddComponent<PlungerSpringBehavior>();

			obj.AddComponent<ConvertToEntity>();
			return ic as PlungerBehavior;
		}
	}
}
