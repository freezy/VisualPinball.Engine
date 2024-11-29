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
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.MetalWireGuide;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Metal Wire Guide")]
	public class MetalWireGuideComponent : MainRenderableComponent<MetalWireGuideData>,
		IMetalWireGuideData
	{
		#region Data

		[Tooltip("Height of the metal wire guide (z-axis).")]
		public float _height = 25f;

		[Min(0)]
		[Tooltip("How thick the metal wire guide band is rendered.")]
		public float _thickness = 3;

		[Min(0)]
		[Tooltip("How tall the metal wire ends are rendered.")]
		public float _standheight = 30f;


		[Tooltip("Rotation on the playfield")]
		public Vector3 Rotation;

		[Tooltip("Radius of the bend")]
		public float _bendradius = 8f;

		[SerializeField]
		private DragPointData[] _dragPoints;

		#endregion

		#region IMetalWireGuideData

		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }
		public float Thickness => _thickness;
		public float Standheight => _standheight;
		public float Height => _height;
		public float RotX => Rotation.x;
		public float RotY => Rotation.y;
		public float RotZ => Rotation.z;
		public float Bendradius => _bendradius;

		#endregion

		#region Overrides

		public override ItemType ItemType => ItemType.MetalWireGuide;
		public override string ItemName => "MetalWireGuide";

		public override MetalWireGuideData InstantiateData() => new MetalWireGuideData();

		public override bool HasProceduralMesh => true;

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<MetalWireGuideData, MetalWireGuideComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<MetalWireGuideData, MetalWireGuideComponent>);

		#endregion

		#region Runtime

		public MetalWireGuideApi MetalWireGuideApi { get; private set; }

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			var physicsEngine = GetComponentInParent<PhysicsEngine>();
			MetalWireGuideApi = new MetalWireGuideApi(gameObject, player, physicsEngine);

			player.Register(MetalWireGuideApi, this);
			RegisterPhysics(physicsEngine);
		}

		#endregion

		#region Transformation

		public override void OnPlayfieldHeightUpdated() => RebuildMeshes();

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(MetalWireGuideData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// geometry
			_height = data.Height;
			Rotation = new Vector3(data.RotX, data.RotY, data.RotZ);
			_thickness = data.Thickness;
			DragPoints = data.DragPoints;
			_bendradius = data.Bendradius;
			_standheight = data.Standheight;

			// collider data
			var collComponent = GetComponent<MetalWireGuideColliderComponent>();
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

		public override IEnumerable<MonoBehaviour> SetReferencedData(MetalWireGuideData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			// mesh
			var mesh = GetComponent<MetalWireGuideMeshComponent>();
			if (mesh) {
				mesh.CreateMesh(data, table, textureProvider, materialProvider);
				mesh.enabled = data.IsVisible;
				SetEnabled<Renderer>(data.IsVisible);
			}

			// collider data
			var collComponent = GetComponentInChildren<MetalWireGuideColliderComponent>();
			if (collComponent) {
				collComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override MetalWireGuideData CopyDataTo(MetalWireGuideData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// update the name
			data.Name = name;

			// geometry
			data.Height = _height;
			data.RotX = Rotation.x;
			data.RotY = Rotation.y;
			data.RotZ = Rotation.z;
			data.Thickness = _thickness;
			data.Bendradius = _bendradius;
			data.DragPoints = DragPoints;

			// visibility
			data.IsVisible = GetEnabled<Renderer>();

			// collision
			var collComponent = GetComponentInChildren<MetalWireGuideColliderComponent>();
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
			var mwgComponent = go.GetComponent<MetalWireGuideComponent>();
			if (mwgComponent != null) {
				_height = mwgComponent._height;
				_thickness = mwgComponent._thickness;
				_standheight = mwgComponent._standheight;
				Rotation = mwgComponent.Rotation;
				_bendradius = mwgComponent._bendradius;
				_dragPoints = mwgComponent._dragPoints.Select(dp => dp.Clone()).ToArray();

			} else {
				MoveDragPointsTo(_dragPoints, go.transform.localPosition.TranslateToVpx());
			}

			UpdateTransforms();
		}

		#endregion
	}
}
