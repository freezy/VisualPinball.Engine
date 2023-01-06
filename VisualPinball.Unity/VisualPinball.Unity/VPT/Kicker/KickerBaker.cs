// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using Unity.Entities;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity
{
	public class KickerBaker : ItemBaker<KickerComponent, KickerData>
	{
		public override void Bake(KickerComponent authoring)
		{
			base.Bake(authoring);
			
			// collision
			var colliderComponent = GetComponent<KickerColliderComponent>();
			if (colliderComponent) {
				AddComponent(new KickerStaticData {
					Center = authoring.Position,
					FallIn = colliderComponent.FallIn,
					FallThrough = colliderComponent.FallThrough,
					HitAccuracy = colliderComponent.HitAccuracy,
					Scatter = colliderComponent.Scatter,
					LegacyMode = true, // todo colliderComponent.LegacyMode,
					ZLow = authoring.Surface?.Height(authoring.Position) ?? authoring.PlayfieldHeight
				});

				AddComponent(new KickerCollisionData {
					BallEntity = Entity.Null,
					LastCapturedBallEntity = Entity.Null
				});

				// if (!Data.LegacyMode) {
				// 	// todo currently we don't allow non-legacy mode
				// 	using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {
				// 		ref var blobAsset = ref blobBuilder.ConstructRoot<KickerMeshVertexBlobAsset>();
				// 		var vertices = blobBuilder.Allocate(ref blobAsset.Vertices, Item.KickerHit.HitMesh.Length);
				// 		var normals = blobBuilder.Allocate(ref blobAsset.Normals, Item.KickerHit.HitMesh.Length);
				// 		for (var i = 0; i < Item.KickerHit.HitMesh.Length; i++) {
				// 			var v = Item.KickerHit.HitMesh[i];
				// 			vertices[i] = new KickerMeshVertex { Vertex = v.ToUnityFloat3() };
				// 			normals[i] = new KickerMeshVertex { Vertex = new float3(KickerHitMesh.Vertices[i].Nx, KickerHitMesh.Vertices[i].Ny, KickerHitMesh.Vertices[i].Nz) };
				// 		}
				//
				// 		var blobAssetReference = blobBuilder.CreateBlobAssetReference<KickerMeshVertexBlobAsset>(Allocator.Persistent);
				// 		dstManager.AddComponentData(entity, new ColliderMeshData { Value = blobAssetReference });
				// 	}
				// }
			}

			// register
			GetComponentInParent<Player>().RegisterKicker(authoring, GetEntity());
		}
	}
}
