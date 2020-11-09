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

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Kicker")]
	public class KickerAuthoring : ItemMainRenderableAuthoring<Kicker, KickerData>,
		ISwitchAuthoring, ICoilAuthoring, IConvertGameObjectToEntity
	{
		protected override Kicker InstantiateItem(KickerData data) => new Kicker(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Kicker, KickerData, KickerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Kicker, KickerData, KickerAuthoring>);

		public override IEnumerable<Type> ValidParents => KickerColliderAuthoring.ValidParentTypes
			.Concat(KickerMeshAuthoring.ValidParentTypes)
			.Distinct();

		public ISwitchable Switchable => Item;

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
				Center = Data.Center.ToUnityFloat2(),
				FallThrough = Data.FallThrough,
				HitAccuracy = Data.HitAccuracy,
				Scatter = Data.Scatter,
				LegacyMode = Data.LegacyMode,
				ZLow = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y) * table.GetScaleZ()
			});
			dstManager.AddComponentData(entity, new KickerCollisionData {
				BallEntity = Entity.Null,
				LastCapturedBallEntity = Entity.Null
			});

			if (!Data.LegacyMode) {
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

		public override void Restore()
		{
			// update the name
			Item.Name = name;

			// visibility is set by the type
			// and it's always collidable
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => Data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Data.Orientation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Data.Orientation = rot.x;
	}
}
