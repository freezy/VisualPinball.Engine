using System.Collections.Generic;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Physics;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
	public class QuadTreeConversionSystem : GameObjectConversionSystem
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void OnUpdate()
		{
			// fixme
			if (DstEntityManager.CreateEntityQuery(typeof(QuadTreeData)).CalculateEntityCount() > 0) {
				return;
			}

			var table = Object.FindObjectOfType<TableAuthoring>().Table;

			foreach (var playable in table.Playables) {
				playable.Init(table);
			}

			// index hittables
			var hitObjects = new List<HitObject>();
			var id = 0;
			foreach (var item in table.Hittables) {
				foreach (var hitObject in item.GetHitShapes()) {
					hitObject.SetIndex(item.Index, item.Version);
					hitObject.Id = id++;
					hitObject.CalcHitBBox();
					hitObjects.Add(hitObject);
				}
			}

			// construct quad tree
			var quadTree = new HitQuadTree(hitObjects, table.BoundingBox);
			var quadTreeBlobAssetRef = QuadTreeBlob.CreateBlobAssetReference(
				quadTree,
				table.GeneratePlayfieldHit(), // todo use `null` if separate playfield mesh exists
				table.GenerateGlassHit()
			);

			// playfield and glass need special treatment, since not part of the quad tree
			var playfieldHitObject = table.GeneratePlayfieldHit();
			var glassHitObject = table.GenerateGlassHit();
			playfieldHitObject.Id = id++;
			glassHitObject.Id = id;
			hitObjects.Add(playfieldHitObject);
			hitObjects.Add(glassHitObject);

			// construct collider blob
			var colliderBlob = ColliderBlob.CreateBlobAssetReference(hitObjects, playfieldHitObject.Id, glassHitObject.Id);

			// save it to entity
			var collEntity = DstEntityManager.CreateEntity(ComponentType.ReadOnly<QuadTreeData>(), ComponentType.ReadOnly<ColliderData>());
			//DstEntityManager.SetName(collEntity, "Collision Data Holder");
			DstEntityManager.SetComponentData(collEntity, new QuadTreeData { Value = quadTreeBlobAssetRef });
			DstEntityManager.SetComponentData(collEntity, new ColliderData { Value = colliderBlob });

			Logger.Info("Static QuadTree initialized.");
		}
	}
}
