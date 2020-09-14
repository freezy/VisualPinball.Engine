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

#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Kicker")]
	public class KickerAuthoring : ItemAuthoring<Kicker, KickerData>, IConvertGameObjectToEntity, IHittableAuthoring, ISwitchableAuthoring
	{
		protected override string[] Children => null;

		protected override Kicker GetItem() => new Kicker(data);

		public IHittable Hittable => Item;

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Kicker>(Name);
			}
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			Item.Init(table);

			dstManager.AddComponentData(entity, new KickerStaticData {
				Center = data.Center.ToUnityFloat2(),
				FallThrough = data.FallThrough,
				HitAccuracy = data.HitAccuracy,
				Scatter = data.Scatter,
				LegacyMode = data.LegacyMode,
				ZLow = table.GetSurfaceHeight(data.Surface, data.Center.X, data.Center.Y) * table.GetScaleZ()
			});
			dstManager.AddComponentData(entity, new KickerCollisionData {
				BallEntity = Entity.Null,
				LastCapturedBallEntity = Entity.Null
			});

			if (!data.LegacyMode) {
				using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {
					ref var blobAsset = ref blobBuilder.ConstructRoot<KickerMeshVertexBlobAsset>();
					var vertices = blobBuilder.Allocate(ref blobAsset.Vertices, Item.KickerHit.HitMesh.Length);
					var normals = blobBuilder.Allocate(ref blobAsset.Normals, Item.KickerHit.HitMesh.Length);
					for (var i = 0; i < Item.KickerHit.HitMesh.Length; i++) {
						var v = Item.KickerHit.HitMesh[i];
						vertices[i] = new KickerMeshVertex { Vertex = v.ToUnityFloat3() };
						normals[i] = new KickerMeshVertex { Vertex = new float3(KickerHitMesh.Vertices[i].Nx, KickerHitMesh.Vertices[i].Ny, KickerHitMesh.Vertices[i].Nz) };
					}

					var blobAssetReference = blobBuilder.CreateBlobAssetReference<KickerMeshVertexBlobAsset>(Allocator.Persistent);
					dstManager.AddComponentData(entity, new ColliderMeshData { Value = blobAssetReference });
				}
			}

			// register
			transform.GetComponentInParent<Player>().RegisterKicker(Item, entity, gameObject);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(data.Orientation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => data.Orientation = rot.x;
	}
}
