using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity
{
	public static class PrimitiveExtensions
	{
		public static PrimitiveAuthoring SetupGameObject(this Primitive primitive, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<PrimitiveAuthoring>().SetItem(primitive);

			obj.AddComponent<ConvertToEntity>();
			return ic as PrimitiveAuthoring;
		}
	}
}
