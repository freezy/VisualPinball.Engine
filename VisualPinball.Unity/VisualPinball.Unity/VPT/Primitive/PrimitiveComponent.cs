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
using MathF = VisualPinball.Engine.Math.MathF;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Primitive")]
	public class PrimitiveComponent : MainRenderableComponent<PrimitiveData>, IMeshGenerator,
		IRotatableComponent
	{
		#region Data

		[Tooltip("Position of the primitive on the playfield.")]
		public Vector3 Position = Vector3.zero;

		[Tooltip("Rotation of the primitive in the playfield coordinate system.")]
		public Vector3 Rotation = Vector3.zero;

		[Tooltip("Scaling of the primitive.")]
		public Vector3 Size = Vector3.one;

		[Tooltip("Translation of the primitive.")]
		public Vector3 Translation = Vector3.zero;

		[Tooltip("Rotation of the primitive after being translated.")]
		public Vector3 ObjectRotation = Vector3.zero;

		#endregion

		#region Overrides

		public override ItemType ItemType => ItemType.Primitive;
		public override string ItemName => "Primitive";

		public override PrimitiveData InstantiateData() => new PrimitiveData();

		public override bool HasProceduralMesh => false;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<PrimitiveData, PrimitiveComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<PrimitiveData, PrimitiveComponent>);

		#endregion

		#region Transformation

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();
			transform.SetFromMatrix(GetTransformationMatrix().ToUnityMatrix().TransformVpxInWorld());
		}

		public float _originalRotateZ;
		public float RotateZ {
			set {
				ObjectRotation.z = _originalRotateZ + value;
				UpdateTransforms();
			}
		}
		public float2 RotatedPosition {
			get => new(Position.x, Position.y);
			set {
				Position.x = value.x;
				Position.y = value.y;
				UpdateTransforms();
			}
		}

		public float4x4 TransformationWithinPlayfield {
			get {
				var scaleMatrix = float4x4.Scale(Size);
				var transMatrix = float4x4.Translate(new float3(Position.x, Position.y, Position.z + PlayfieldHeight));
				var rotTransMatrix = math.mul(
					float4x4.EulerZYX(math.radians(Rotation)),
					float4x4.Translate(Translation)
				);
				rotTransMatrix = math.mul(
					float4x4.EulerZYX(math.radians(ObjectRotation)),
					rotTransMatrix
				);
				return math.mul(transMatrix, math.mul(rotTransMatrix, scaleMatrix));
			}
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(PrimitiveData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Position.ToUnityVector3();
			Size = data.Size.ToUnityFloat3();
			Rotation = new Vector3(data.RotAndTra[0], data.RotAndTra[1], data.RotAndTra[2]);
			Translation = new Vector3(data.RotAndTra[3], data.RotAndTra[4], data.RotAndTra[5]);
			ObjectRotation = new Vector3(data.RotAndTra[6], data.RotAndTra[7], data.RotAndTra[8]);

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
			data.Name = name;
			data.Position = Position.ToVertex3D();
			data.Size = Size.ToVertex3D();
			data.RotAndTra = new[] {
				Rotation.x, Rotation.y, Rotation.z,
				Translation.x, Translation.y, Translation.z,
				ObjectRotation.x, ObjectRotation.y, ObjectRotation.z,
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
			if (primitiveComponent != null) {

				Position = primitiveComponent.Position;
				Rotation = primitiveComponent.Rotation;
				Size = primitiveComponent.Size;
				Translation = primitiveComponent.Translation;
				ObjectRotation = primitiveComponent.ObjectRotation;

			} else {
				Position = go.transform.localPosition.TranslateToVpx();
				Rotation = go.transform.localEulerAngles;
				Size = go.transform.localScale;
			}

			UpdateTransforms();
		}

		#endregion

		#region Runtime

		public PrimitiveApi PrimitiveApi { get; private set; }

		private void Awake()
		{
			_originalRotateZ = ObjectRotation.z;

			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			PrimitiveApi = new PrimitiveApi(gameObject, player, physicsEngine);

			player.Register(PrimitiveApi, this);
			RegisterPhysics(physicsEngine);
		}

		#endregion

		#region IMeshGenerator

		public Mesh GetMesh() => GetDefaultMesh();

		public Matrix3D GetTransformationMatrix()
		{
			// scale matrix
			var scaleMatrix = new Matrix3D();
			scaleMatrix.SetScaling(Size.x, Size.y, Size.z);

			// translation matrix
			var tableHeight = PlayfieldHeight;
			var transMatrix = new Matrix3D();
			transMatrix.SetTranslation(Position.x, Position.y, Position.z + tableHeight);

			// translation + rotation matrix
			var rotTransMatrix = new Matrix3D();
			rotTransMatrix.SetTranslation(Translation.x, Translation.y, Translation.z);

			var tempMatrix = new Matrix3D();
			tempMatrix.RotateZMatrix(MathF.DegToRad(Rotation.z));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateYMatrix(MathF.DegToRad(Rotation.y));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateXMatrix(MathF.DegToRad(Rotation.x));
			rotTransMatrix.Multiply(tempMatrix);

			tempMatrix.RotateZMatrix(MathF.DegToRad(ObjectRotation.z));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateYMatrix(MathF.DegToRad(ObjectRotation.y));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateXMatrix(MathF.DegToRad(ObjectRotation.x));
			rotTransMatrix.Multiply(tempMatrix);

			var fullMatrix = scaleMatrix.Clone();
			fullMatrix.Multiply(rotTransMatrix);
			fullMatrix.Multiply(transMatrix); // fullMatrix = Smatrix * RTmatrix * Tmatrix
			scaleMatrix.SetScaling(1.0f, 1.0f, 1.0f);
			fullMatrix.Multiply(scaleMatrix);

			return fullMatrix;
		}

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => Position;
		public override void SetEditorPosition(Vector3 pos) => Position = pos;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorRotation() => Rotation;
		public override void SetEditorRotation(Vector3 rot) => Rotation = rot;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale() => Size;
		public override void SetEditorScale(Vector3 scale) => Size = scale;

		#endregion
	}
}
