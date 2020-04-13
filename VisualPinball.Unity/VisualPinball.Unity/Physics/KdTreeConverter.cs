using System.Collections.Generic;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.HitTest;
using VisualPinball.Unity.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Physics
{
	public class KdTreeConverter : GameObjectConversionSystem
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void OnUpdate()
		{
			var table = Object.FindObjectOfType<TableBehavior>().Table;

			foreach (var playable in table.Playables) {
				playable.SetupPlayer(null, table);
			}

			// index hittables
			var hitObjects = new List<HitObject>();
			foreach (var hittable in table.Hittables) {
				foreach (var hitObject in hittable.GetHitShapes()) {
					hitObjects.Add(hitObject);
					hitObject.CalcHitBBox();
				}
			}
			var kdTree = new HitQuadTree(hitObjects, table.Data.BoundingBox);

			var bbpSystem = DstEntityManager.World.GetOrCreateSystem<BallBroadPhaseSystem>();
			bbpSystem.KdTree = kdTree;

			Logger.Info("KdTree converted.");
		}
	}
}
