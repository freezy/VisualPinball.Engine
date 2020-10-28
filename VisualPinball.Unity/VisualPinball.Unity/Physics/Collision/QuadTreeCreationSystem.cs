// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Physics;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	internal static class QuadTreeCreationSystem
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static void Create(EntityManager entityManager)
		{
			var table = Object.FindObjectOfType<TableAuthoring>().Table;
			var stopWatch = new Stopwatch();

			stopWatch.Start();
			foreach (var playable in table.Playables) {
				playable.Init(table);
			}

			// index hittables
			var hittables = table.Hittables.Where(hittable => hittable.IsCollidable).ToArray();
			var hitObjects = new List<HitObject>();
			var id = 0;
			var log = "";
			var c = 0;

			foreach (var item in hittables) {
				var hitShapes = item.GetHitShapes();
				log += item.Name + ": " + hitShapes.Length + "\n";
				c += hitShapes.Length;
				foreach (var hitObject in hitShapes) {
					hitObject.SetIndex(item.Index, item.Version, item.ParentIndex, item.ParentVersion);
					hitObject.Id = id++;
					hitObject.CalcHitBBox();
					hitObjects.Add(hitObject);
				}
			}
			stopWatch.Stop();
			Logger.Info("Collider Count:\n" + log + "\nTotal: " + c + " colliders in " + stopWatch.ElapsedMilliseconds + "ms");

			// construct quad tree
			var quadTree = new Engine.Physics.QuadTree(hitObjects, table.BoundingBox);
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
			var collEntity = entityManager.CreateEntity(ComponentType.ReadOnly<QuadTreeData>(), ComponentType.ReadOnly<ColliderData>());
			//DstEntityManager.SetName(collEntity, "Collision Data Holder");
			entityManager.SetComponentData(collEntity, new QuadTreeData { Value = quadTreeBlobAssetRef });
			entityManager.SetComponentData(collEntity, new ColliderData { Value = colliderBlob });

			Logger.Info("Static QuadTree initialized.");
		}
	}
}
