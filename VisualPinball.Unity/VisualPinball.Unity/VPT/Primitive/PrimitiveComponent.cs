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

#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Primitive")]
	public class PrimitiveComponent : MainRenderableComponent<PrimitiveData>, IMeshGenerator
	{
		#region Data

		public Vector3 Position {
			get => transform.localPosition.TranslateToVpx();
			set => transform.localPosition = value.TranslateToWorld();
		}

		#endregion

		#region Overrides

		public override ItemType ItemType => ItemType.Primitive;
		public override string ItemName => "Primitive";

		public override PrimitiveData InstantiateData() => new();

		public override bool HasProceduralMesh => false;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<PrimitiveData, PrimitiveComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<PrimitiveData, PrimitiveComponent>);

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(PrimitiveData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			var position = data.Position.ToUnityVector3();
			var size = data.Size.ToUnityFloat3();
			var rotation = new Vector3(data.RotAndTra[0], data.RotAndTra[1], data.RotAndTra[2]);
			var translation = new Vector3(data.RotAndTra[3], data.RotAndTra[4], data.RotAndTra[5]);
			var objectRotation = new Vector3(data.RotAndTra[6], data.RotAndTra[7], data.RotAndTra[8]);

			var scaleMatrix = float4x4.Scale(size);
			var transMatrix = float4x4.Translate(position);
			var rotTransMatrix = math.mul(
				float4x4.EulerZYX(math.radians(objectRotation)),
				math.mul(
					float4x4.EulerZYX(math.radians(rotation)),
					float4x4.Translate(translation)
				));
			var transformationWithinPlayfieldMatrix = math.mul(transMatrix, math.mul(rotTransMatrix, scaleMatrix));
			transform.SetFromMatrix(((Matrix4x4)transformationWithinPlayfieldMatrix).TransformVpxInWorld());

			// mesh
			var meshComponent = GetComponent<PrimitiveMeshComponent>();
			if (meshComponent) {
				meshComponent.Sides = data.Sides;
				meshComponent.UseLegacyMesh = !data.Use3DMesh;
			}

			// collider
			var collComponent = GetComponent<PrimitiveColliderComponent>();
			if (collComponent) {

				if (data.IsToy) {
					DestroyImmediate(collComponent);
				} else {
					collComponent.enabled = data.IsCollidable;

					collComponent.HitEvent = data.HitEvent;
					collComponent.Threshold = data.Threshold;
					collComponent.Elasticity = data.Elasticity;
					collComponent.ElasticityFalloff = data.ElasticityFalloff;
					collComponent.Friction = data.Friction;
					collComponent.Scatter = data.Scatter;
					collComponent.CollisionReductionFactor = data.CollisionReductionFactor;
					collComponent.OverwritePhysics = data.OverwritePhysics;

					updatedComponents.Add(collComponent);
				}
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(PrimitiveData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// mesh
			var meshComponent = GetComponent<PrimitiveMeshComponent>();
			if (meshComponent) {
				meshComponent.CreateMesh(data, table, textureProvider, materialProvider);
				meshComponent.enabled = data.IsVisible;
				SetEnabled<Renderer>(data.IsVisible);

				updatedComponents.Add(meshComponent);
			}

			return updatedComponents;
		}

		public override PrimitiveData CopyDataTo(PrimitiveData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			var t = transform;
			data.Name = name;
			data.Position = Position.ToVertex3D();
			data.Size = t.localScale.ToVertex3D();
			var vpxRotation = t.localEulerAngles.TranslateToVpx();
			data.RotAndTra = new[] {
				vpxRotation.x, vpxRotation.y, vpxRotation.z,
				0, 0, 0,
				0, 0, 0,
			};

			// materials
			var mr = GetComponent<MeshRenderer>();
			if (mr) {
				CopyMaterialName(mr, materialNames, textureNames, ref data.Material, ref data.Image, ref data.NormalMap);
			}

			// mesh
			var meshComponent = GetComponent<PrimitiveMeshComponent>();
			if (meshComponent) {
				data.IsVisible = GetEnabled<Renderer>();
				data.Sides = meshComponent.Sides;
				data.Use3DMesh = !meshComponent.UseLegacyMesh;

				if (forExport && !meshComponent.UseLegacyMesh) {
					var mf = GetComponent<MeshFilter>();
					if (mf) {
						data.Mesh = mf.sharedMesh.ToVpMesh().TransformToVpx();
						data.NumIndices = data.Mesh.Indices.Length;
						data.NumVertices = data.Mesh.Vertices.Length;
					}
				}
			}

			// update collision
			// todo at some point we need to be able to toggle collidable during gameplay,
			// todo but for now let's keep things static.
			var collComponent = GetComponent<PrimitiveColliderComponent>();
			if (collComponent) {
				data.IsCollidable = collComponent.enabled;
				data.IsToy = false;

				data.HitEvent = collComponent.HitEvent;
				data.Threshold = collComponent.Threshold;
				data.Elasticity = collComponent.Elasticity;
				data.ElasticityFalloff = collComponent.ElasticityFalloff;
				data.Friction = collComponent.Friction;
				data.Scatter = collComponent.Scatter;
				data.CollisionReductionFactor = collComponent.CollisionReductionFactor;
				data.OverwritePhysics = collComponent.OverwritePhysics;

			} else {
				data.IsCollidable = false;
				data.IsToy = true;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var primitiveComponent = go.GetComponent<PrimitiveComponent>();
			var targetTransform = transform;
			var sourceTransform = primitiveComponent != null ? primitiveComponent.transform : go.transform;

			targetTransform.localPosition = sourceTransform.localPosition;
			targetTransform.localRotation = sourceTransform.localRotation;
			targetTransform.localScale = sourceTransform.localScale;

			UpdateTransforms();
		}

		#endregion

		#region Runtime

		public PrimitiveApi PrimitiveApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			PrimitiveApi = new PrimitiveApi(gameObject, player, physicsEngine);

			player.Register(PrimitiveApi, this);
			RegisterPhysics(physicsEngine);
		}

		#endregion

		#region IMeshGenerator

		public Mesh GetMesh() => GetDefaultMesh();

		#endregion
	}
}
