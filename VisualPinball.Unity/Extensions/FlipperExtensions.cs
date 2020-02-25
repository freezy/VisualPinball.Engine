using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Components;
using VisualPinball.Unity.Physics.Flipper;

namespace VisualPinball.Unity.Extensions
{
	public static class FlipperExtensions
	{
		public static void SetupGameObject(this Flipper flipper, GameObject obj, RenderObjectGroup rog)
		{
			obj.AddComponent<VisualPinballFlipper>().SetData(flipper.Data);
			obj.AddComponent<ConvertToEntity>();
			rog.Get(FlipperMeshGenerator.RubberName).AddPhysicsShape(obj);
		}
	}
}
