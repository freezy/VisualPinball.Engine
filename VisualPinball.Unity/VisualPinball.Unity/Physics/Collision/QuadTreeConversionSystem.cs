using System.Collections.Generic;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Physics.Collision
{
	[UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
	public class QuadTreeConversionSystem : GameObjectConversionSystem
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override unsafe void OnUpdate()
		{
			// fixme
			if (DstEntityManager.CreateEntityQuery(typeof(ColliderData)).CalculateEntityCount() > 0) {
				return;
			}

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
			var quadTreeBlobAssetRef = QuadTreeBlob.CreateBlobAssetReference(
				quadTree,
				table.GeneratePlayfieldHit(), // todo use `null` if separate playfield mesh exists
				table.GenerateGlassHit()
			);

			ref var collider4 = ref quadTreeBlobAssetRef.Value.PlayfieldCollider.Value;
			Debug.Log("Playfield Collider: " + Collider.Collider.ToString(ref collider4));
			fixed (Collider.Collider* collider = &collider4) {
				Debug.Log("Playfield Collider Normal: " + ((PlaneCollider*)collider)->Normal);
			}

			// save it to entity
			var collEntity = DstEntityManager.CreateEntity(ComponentType.ReadOnly<ColliderData>());
			DstEntityManager.SetName(collEntity, "Collision Holder");
			DstEntityManager.SetComponentData(collEntity, new ColliderData { Colliders = quadTreeBlobAssetRef });

			Logger.Info("Static QuadTree initialized.");
		}
	}
}
