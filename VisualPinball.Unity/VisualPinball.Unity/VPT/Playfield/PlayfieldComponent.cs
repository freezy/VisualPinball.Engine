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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using NativeTrees;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Playfield;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Playfield")]
	public class PlayfieldComponent : MainRenderableComponent<TableData>
	{
		#region Data

		[Tooltip("Height of the glass above the playfield. Serves as outer collision bounds of the Z-axis.")]
		public float GlassHeight;

		[Tooltip("Left position of the playfield (X-axis value).")]
		public float Left;

		[Tooltip("Width of the playfield.")]
		public float Right = 952f;

		[Tooltip("Top position of the playfield (Y-axis value).")]
		public float Top;

		[Tooltip("Height of the playfield (Y-axis size).")]
		public float Bottom = 2162f;

		public float AngleTiltMax = 6f;

		public float AngleTiltMin = 6f;

		[Tooltip("How much the playfield should be rotated during runtime (in edit time, we keep it horizontal)")]
		public float RenderSlope = 3.6f;

		public new int PlayfieldDetailLevel = 10;

		[SerializeField] private string _playfieldImage;
		[SerializeField] private string _playfieldMaterial;

		#endregion

		public float Width => Right;
		public float Height => Bottom;

		public override ItemType ItemType => ItemType.Playfield;
		public override string ItemName => "Playfield";

		public override bool HasProceduralMesh => true;

		public override TableData InstantiateData() => new TableData();

		protected override Type MeshComponentType => typeof(PlayfieldMeshComponent);
		protected override Type ColliderComponentType => typeof(PlayfieldColliderComponent);

		public Rect3D BoundingBox => new Rect3D(Left, Right, Top, Bottom, 0, GlassHeight);
		public Aabb Bounds => new Aabb(Left, Right, Top, Bottom, 0, GlassHeight);
		public AABB2D Bounds2D => new AABB2D(new float2(Left, Top), new float2(Right, Bottom));

		public float3 PlayfieldGravity(float strength) {
			var tableComponent = GetComponentInParent<TableComponent>();
			var difficulty = tableComponent ? tableComponent.GlobalDifficulty : 0.2f;
			var slope = AngleTiltMin + (AngleTiltMax - AngleTiltMin) * difficulty;
			return new float3(0, math.sin(math.radians(slope)) * strength, -math.cos(math.radians(slope)) * strength);
		}

		public PlayfieldApi PlayfieldApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			PlayfieldApi = new PlayfieldApi(gameObject, player, physicsEngine);
			player.Register(PlayfieldApi);

			transform.RotateAround(Vector3.zero, Vector3.right, -RenderSlope);
		}

		public override IEnumerable<MonoBehaviour> SetData(TableData data)
		{
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			var updatedComponents = new List<MonoBehaviour> { this };

			// position
			GlassHeight = data.GlassHeight;
			Left = data.Left;
			Left = data.Left;
			Left = data.Left;
			Right = data.Right;
			Top = data.Top;
			Bottom = data.Bottom;
			AngleTiltMax = data.AngleTiltMax;
			AngleTiltMin = data.AngleTiltMin;
			if (physicsEngine) {
				physicsEngine.GravityStrength = data.Gravity;
			}

			// playfield material
			_playfieldImage = data.Image;
			_playfieldMaterial = data.PlayfieldMaterial;

			// collider data
			var collComponent = GetComponent<PlayfieldColliderComponent>();
			if (collComponent) {
				collComponent.Gravity = data.Gravity;
				collComponent.Elasticity = data.Elasticity;
				collComponent.ElasticityFalloff = data.ElasticityFalloff;
				collComponent.Friction = data.Friction;
				collComponent.Scatter = data.Scatter;
				collComponent.DefaultScatter = data.DefaultScatter;

				updatedComponents.Add(collComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(TableData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			var meshComponent = GetComponentInChildren<PlayfieldMeshComponent>();
			if (meshComponent && meshComponent.AutoGenerate) {
				meshComponent.CreateMesh(data, table, textureProvider, materialProvider);
			}
			return Array.Empty<MonoBehaviour>();
		}

		public IEnumerable<MonoBehaviour> SetReferencedData(PrimitiveData primitiveData, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider)
		{
			var mf = GetComponent<MeshFilter>();
			var playfieldMeshComponent = GetComponent<PlayfieldMeshComponent>();
			if (!mf || !playfieldMeshComponent) {
				return Array.Empty<MonoBehaviour>();
			}

			var updatedComponents = new List<MonoBehaviour> { this };
			var mg = new PrimitiveMeshGenerator(primitiveData);
			var mesh = mg
				.GetTransformedMesh(table?.TableHeight ?? 0f, primitiveData.Mesh, Origin.Original, false)
				.Transform(mg.TransformationMatrix(0)) // apply transformation to mesh, because this is the playfield
				.TransformToWorld(); // also, transform this to world space.
			var material = new PbrMaterial(
				table?.GetMaterial(_playfieldMaterial),
				table?.GetTexture(_playfieldImage)
			);
			MeshComponent<PrimitiveData, PrimitiveComponent>.CreateMesh(gameObject, mesh, material, "playfield_mesh", textureProvider, materialProvider);
			playfieldMeshComponent.AutoGenerate = false;

			updatedComponents.Add(playfieldMeshComponent);

			return updatedComponents;
		}

		public override TableData CopyDataTo(TableData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			var physicsEngine = GetComponentInParent<PhysicsEngine>();

			// position
			data.TableHeight = 0;
			data.GlassHeight = GlassHeight;
			data.Left = Left;
			data.Right = Right;
			data.Top = Top;
			data.Bottom = Bottom;
			data.AngleTiltMax = AngleTiltMax;
			data.AngleTiltMin = AngleTiltMin;
			if (physicsEngine) {
				data.Gravity = physicsEngine.GravityStrength;
			}

			// playfield material
			data.Image = _playfieldImage;
			data.PlayfieldMaterial = _playfieldMaterial;

			// collider data
			var collComponent = GetComponent<PlayfieldColliderComponent>();
			if (collComponent) {
				data.Gravity = collComponent.Gravity;
				data.Elasticity = collComponent.Elasticity;
				data.ElasticityFalloff = collComponent.ElasticityFalloff;
				data.Friction = collComponent.Friction;
				data.Scatter = collComponent.Scatter;
				data.DefaultScatter = collComponent.DefaultScatter;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			throw new Exception("Copying object data is currently only used for replacing objects. Don't replace the playfield. Refactor this if necessary in the future.");
		}
	}
}
