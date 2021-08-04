// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
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
		#region Data

		public float Radius = 25f;

		public float Orientation;

		public float Scatter;

		public float HitAccuracy = 0.7f;

		public float HitHeight = 40.0f;

		public SurfaceAuthoring Surface;

		public bool FallThrough;

		public bool LegacyMode = true;

		public float Angle = 90f;

		[Tooltip("Speed the kicker hits the ball when ejecting.")]
		[Range(0f, 100f)]
		public float Speed = 3f;

		#endregion

		protected override Kicker InstantiateItem(KickerData data) => new Kicker(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Kicker, KickerData, KickerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Kicker, KickerData, KickerAuthoring>);

		public override IEnumerable<Type> ValidParents => KickerColliderAuthoring.ValidParentTypes
			.Concat(KickerMeshAuthoring.ValidParentTypes)
			.Distinct();

		public ISwitchable Switchable => Item;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;

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
				// todo currently we don't allow non-legacy mode
				// using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {
				// 	ref var blobAsset = ref blobBuilder.ConstructRoot<KickerMeshVertexBlobAsset>();
				// 	var vertices = blobBuilder.Allocate(ref blobAsset.Vertices, Item.KickerHit.HitMesh.Length);
				// 	var normals = blobBuilder.Allocate(ref blobAsset.Normals, Item.KickerHit.HitMesh.Length);
				// 	for (var i = 0; i < Item.KickerHit.HitMesh.Length; i++) {
				// 		var v = Item.KickerHit.HitMesh[i];
				// 		vertices[i] = new KickerMeshVertex { Vertex = v.ToUnityFloat3() };
				// 		normals[i] = new KickerMeshVertex { Vertex = new float3(KickerHitMesh.Vertices[i].Nx, KickerHitMesh.Vertices[i].Ny, KickerHitMesh.Vertices[i].Nz) };
				// 	}
				//
				// 	var blobAssetReference = blobBuilder.CreateBlobAssetReference<KickerMeshVertexBlobAsset>(Allocator.Persistent);
				// 	dstManager.AddComponentData(entity, new ColliderMeshData { Value = blobAssetReference });
				// }
			}

			// register
			transform.GetComponentInParent<Player>().RegisterKicker(Item, entity, ParentEntity, gameObject);
		}

		public override void SetData(KickerData data, IMaterialProvider materialProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			Orientation = data.Orientation;
			Radius = data.Radius;
			Scatter = data.Scatter;
			HitAccuracy = data.HitAccuracy;
			HitHeight = data.HitHeight;
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);
			FallThrough = data.FallThrough;
			LegacyMode = data.LegacyMode;
			Angle = data.Angle;
			Speed = data.Speed;
		}

		public override void CopyDataTo(KickerData data)
		{
			var localPos = transform.localPosition;

			// update the name
			data.Name = name;

			// todo visibility is set by the type

			// other props
			data.Orientation = Orientation;
			data.Radius = Radius;
			data.Scatter = Scatter;
			data.HitAccuracy = HitAccuracy;
			data.HitHeight = HitHeight;
			data.Surface = Surface ? Surface.name : string.Empty;
			data.FallThrough = FallThrough;
			data.LegacyMode = LegacyMode;
			data.Angle = Angle;
			data.Speed = Speed;
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Orientation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Orientation = rot.x;
	}
}
