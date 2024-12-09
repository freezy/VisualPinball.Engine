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
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Rubber")]
	public class RubberComponent : MainRenderableComponent<RubberData>,
		IRubberData
	{
		#region Data

		[Tooltip("Height of the rubber (z-axis).")]
		public float _height = 25f;

		[Min(0)]
		[Tooltip("How thick the rubber band is rendered.")]
		public int _thickness = 8;

		[Tooltip("Rotation on the playfield")]
		public Vector3 Rotation;

		[SerializeField]
		private DragPointData[] _dragPoints;

		[NonSerialized]
		private float _scale = 1f;

		[NonSerialized]
		private Vertex3D[] _scalingDragPoints;

		[NonSerialized]
		private float4x4 _playfieldToWorld;

		#endregion

		#region IRubberData

		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }
		public int Thickness => math.max(1, _thickness); // don't allow zero thickness
		public float Height => _height;
		public float RotX => Rotation.x;
		public float RotY => Rotation.y;
		public float RotZ => Rotation.z;

		#endregion

		#region Overrides

		public override ItemType ItemType => ItemType.Rubber;
		public override string ItemName => "Rubber";

		public override RubberData InstantiateData() => new RubberData();

		public override bool HasProceduralMesh => true;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<RubberData, RubberComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<RubberData, RubberComponent>);

		#endregion

		#region Runtime

		public RubberApi RubberApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			RubberApi = new RubberApi(gameObject, player, physicsEngine);

			player.Register(RubberApi, this);
			RegisterPhysics(physicsEngine);
		}

		private void Start()
		{
			var playfield = GetComponentInParent<PlayfieldComponent>();
			_playfieldToWorld = playfield ? playfield.transform.localToWorldMatrix : float4x4.identity;
		}

		#endregion

		#region Transformation

		public float4x4 TransformationWithinPlayfield => transform.worldToLocalMatrix.WorldToLocalTranslateWithinPlayfield(_playfieldToWorld);

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(RubberData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// geometry
			_height = data.Height;
			Rotation = new Vector3(data.RotX, data.RotY, data.RotZ);
			_thickness = data.Thickness;
			DragPoints = data.DragPoints;

			// collider data
			var collComponent = GetComponent<RubberColliderComponent>();
			if (collComponent) {
				collComponent.enabled = data.IsCollidable;

				collComponent.HitEvent = data.HitEvent;
				collComponent.HitHeight = data.HitHeight;
				collComponent.OverwritePhysics = data.OverwritePhysics;
				collComponent.Elasticity = data.Elasticity;
				collComponent.ElasticityFalloff = data.ElasticityFalloff;
				collComponent.Friction = data.Friction;
				collComponent.Scatter = data.Scatter;

				updatedComponents.Add(collComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(RubberData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			// mesh
			var mesh = GetComponent<RubberMeshComponent>();
			if (mesh) {
				mesh.CreateMesh(data, table, textureProvider, materialProvider);
				mesh.enabled = data.IsVisible;
				SetEnabled<Renderer>(data.IsVisible);
			}

			// collider data
			var collComponent = GetComponentInChildren<RubberColliderComponent>();
			if (collComponent) {
				collComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override RubberData CopyDataTo(RubberData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// update the name
			data.Name = name;

			// geometry
			data.Height = _height;
			data.RotX = Rotation.x;
			data.RotY = Rotation.y;
			data.RotZ = Rotation.z;
			data.Thickness = _thickness;
			data.DragPoints = DragPoints;

			// visibility
			data.IsVisible = GetEnabled<Renderer>();

			// collision
			var collComponent = GetComponentInChildren<RubberColliderComponent>();
			if (collComponent) {
				data.IsCollidable = collComponent.enabled;

				data.HitEvent = collComponent.HitEvent;
				data.HitHeight = collComponent.HitHeight;

				data.PhysicsMaterial = collComponent.PhysicsMaterial ? collComponent.PhysicsMaterial.name : string.Empty;
				data.OverwritePhysics = collComponent.OverwritePhysics;
				data.Elasticity = collComponent.Elasticity;
				data.ElasticityFalloff = collComponent.ElasticityFalloff;
				data.Friction = collComponent.Friction;
				data.Scatter = collComponent.Scatter;

			} else {
				data.IsCollidable = false;
			}

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var srcMainComp = go.GetComponent<RubberComponent>();
			if (srcMainComp) {
				_height = srcMainComp._height;
				_thickness = srcMainComp._thickness;
				Rotation = srcMainComp.Rotation;
				_height = srcMainComp._height;
				_dragPoints = srcMainComp._dragPoints.Select(dp => dp.Clone()).ToArray();

			}
			RebuildMeshes();
		}

		#endregion
	}
}
