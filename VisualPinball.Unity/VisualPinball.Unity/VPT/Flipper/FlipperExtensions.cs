using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Flipper
{
	public static class FlipperExtensions
	{
		public static void SetupGameObject(this Engine.VPT.Flipper.Flipper flipper, GameObject obj, RenderObjectGroup rog)
		{
			obj.AddComponent<FlipperBehavior>().SetData(flipper.Data);
			obj.AddComponent<ConvertToEntity>();
			//rog.AddPhysicsShape(obj);
			//rog.Get(FlipperMeshGenerator.RubberName).AddPhysicsShape(obj);
			rog.Get(FlipperMeshGenerator.RubberName).AddPhysicsShapeToParent(obj);
		}
	}
}
