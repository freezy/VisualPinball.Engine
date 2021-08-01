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
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Primitive")]
	public class PrimitiveAuthoring : ItemMainRenderableAuthoring<Primitive, PrimitiveData>, IConvertGameObjectToEntity
	{
		#region Data

		public bool HitEvent = true;

		public float Threshold = 2f;

		public float Elasticity = 0.3f;

		public float ElasticityFalloff = 0.5f;

		public float Friction = 0.3f;

		public float Scatter;

		public float EdgeFactorUi = 0.25f;

		public float CollisionReductionFactor = 0;

		public bool IsToy;

		public bool OverwritePhysics = true;

		public bool StaticRendering = true;

		#endregion

		public override bool IsCollidable => !Data.IsToy;

		protected override Primitive InstantiateItem(PrimitiveData data) => new Primitive(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Primitive, PrimitiveData, PrimitiveAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Primitive, PrimitiveData, PrimitiveAuthoring>);

		public override IEnumerable<Type> ValidParents => PrimitiveColliderAuthoring.ValidParentTypes
			.Concat(PrimitiveMeshAuthoring.ValidParentTypes)
			.Distinct();

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// register
			var primitive = GetComponent<PrimitiveAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterPrimitive(primitive, entity, ParentEntity, gameObject);
		}

		public override void SetData(PrimitiveData data, Dictionary<string, IItemMainAuthoring> itemMainAuthorings)
		{
			HitEvent = data.HitEvent;
			Threshold = data.Threshold;
			Elasticity = data.Elasticity;
			ElasticityFalloff = data.ElasticityFalloff;
			Friction = data.Friction;
			Scatter = data.Scatter;
			EdgeFactorUi = data.EdgeFactorUi;
			CollisionReductionFactor = data.CollisionReductionFactor;
			IsToy = data.IsToy;
			OverwritePhysics = data.OverwritePhysics;
			StaticRendering = data.StaticRendering;
		}

		public override void CopyDataTo(PrimitiveData data)
		{
			var localPos = transform.localPosition;

			// name and position
			data.Name = name;
			data.Position = localPos.ToVertex3D();

			// update visibility
			data.IsVisible = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case PrimitiveMeshAuthoring meshAuthoring:
						data.IsVisible = meshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// update collision
			// todo at some point we need to be able to toggle collidable during gameplay,
			// todo but for now let's keep things static.
			data.IsToy = true;
			data.IsCollidable = false;
			foreach (var colliderComponent in ColliderComponents) {
				if (colliderComponent is PrimitiveColliderAuthoring colliderAuthoring) {
					var active = colliderAuthoring.gameObject.activeInHierarchy;
					data.IsCollidable = active;
					data.IsToy = !active;
				}
			}

			// other props
			data.HitEvent = HitEvent;
			data.Threshold = Threshold;
			data.Elasticity = Elasticity;
			data.ElasticityFalloff = ElasticityFalloff;
			data.Friction = Friction;
			data.Scatter = Scatter;
			data.EdgeFactorUi = EdgeFactorUi;
			data.CollisionReductionFactor = CollisionReductionFactor;
			data.IsToy = IsToy;
			data.OverwritePhysics = OverwritePhysics;
			data.StaticRendering = StaticRendering;
		}

		public override void FillBinaryData()
		{
			var meshAuth = GetComponent<PrimitiveMeshAuthoring>();
			if (!meshAuth) {
				meshAuth = GetComponentInChildren<PrimitiveMeshAuthoring>();
			}

			var meshGo = meshAuth ? meshAuth.gameObject : gameObject;
			var mf = meshGo.GetComponent<MeshFilter>();
			if (mf) {
				Data.Mesh = mf.sharedMesh.ToVpMesh();
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override void SetEditorPosition(Vector3 pos) => Data.Position = pos.ToVertex3D();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorRotation() => new Vector3(Data.RotAndTra[0], Data.RotAndTra[1], Data.RotAndTra[2]);
		public override void SetEditorRotation(Vector3 rot)
		{
			Data.RotAndTra[0] = rot.x;
			Data.RotAndTra[1] = rot.y;
			Data.RotAndTra[2] = rot.z;
		}

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale() => Data.Size.ToUnityVector3();
		public override void SetEditorScale(Vector3 scale) => Data.Size = scale.ToVertex3D();
	}
}
