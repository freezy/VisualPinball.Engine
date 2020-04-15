using System.Collections.Generic;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.HitTest;
using VisualPinball.Unity.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Physics
{
	[UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
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
					hitObject.ItemIndex = hittable.Index;
					hitObjects.Add(hitObject);
					hitObject.CalcHitBBox();
				}
			}

			// construct quad tree
			var quadTree = new HitQuadTree(hitObjects, table.Data.BoundingBox);
			var quadTreeBlobAssetRef = QuadTree.CreateBlobAssetReference(quadTree);

			// assign it to system
			var bbpSystem = DstEntityManager.World.GetOrCreateSystem<BallBroadPhaseSystem>();
			DstEntityManager.CreateEntity(ComponentType.ReadOnly<CollisionData>());
			bbpSystem.SetSingleton(new CollisionData { QuadTree = quadTreeBlobAssetRef });
		}
	}
}
