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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Kicker")]
	public class KickerAuthoring : ItemMainRenderableAuthoring<KickerData>,
		ICoilDeviceAuthoring, ITriggerAuthoring, IBallCreationPosition, IOnSurfaceAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Position of the kicker on the playfield.")]
		public Vector2 Position;

		[Tooltip("Kicker radius. Scales the mesh accordingly.")]
		public float Radius = 25f;

		[Tooltip("R-Rotation of the kicker")]
		public float Orientation;

		public ISurfaceAuthoring Surface { get => _surface as ISurfaceAuthoring; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceAuthoring), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface the kicker is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

		[HideInInspector]
		public int KickerType;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Kicker;
		public override string ItemName => "Kicker";

		public override IEnumerable<Type> ValidParents => KickerColliderAuthoring.ValidParentTypes
			.Distinct();

		public override KickerData InstantiateData() => new KickerData();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<KickerData, KickerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<KickerData, KickerAuthoring>);

		public Vector2 Center => Position;

		public const string SwitchItem = "kicker_switch";

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem),
		};

		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			// todo support multiple coils, also see plunger which has 2 coil definitions
			new GamelogicEngineCoil("c_1")
		};

		IEnumerable<GamelogicEngineCoil> IDeviceAuthoring<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<GamelogicEngineSwitch> IDeviceAuthoring<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#endregion

		#region Transformation

		public void OnSurfaceUpdated() => UpdateTransforms();
		public float PositionZ => SurfaceHeight(Surface, Position);


		public override void UpdateTransforms()
		{
			var t = transform;

			// position
			t.localPosition = new Vector3(Position.x, Position.y, PositionZ);

			if (KickerType == Engine.VPT.KickerType.KickerCup) {
				t.localPosition += new Vector3(0, 0, -0.18f * Radius);
			}

			// scale
			t.localScale = new Vector3(Radius, Radius, Radius);

			// rotation
			t.localEulerAngles = KickerType switch {
				Engine.VPT.KickerType.KickerCup => new Vector3(0, 0, Orientation),
				Engine.VPT.KickerType.KickerWilliams => new Vector3(0, 0, Orientation + 90f),
				_ => t.localEulerAngles
			};
		}

		#endregion

		#region Conversion

			public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// collision
			var colliderAuthoring = gameObject.GetComponent<KickerColliderAuthoring>();
			if (colliderAuthoring) {
				dstManager.AddComponentData(entity, new KickerStaticData {
					Center = Position,
					FallThrough = colliderAuthoring.FallThrough,
					HitAccuracy = colliderAuthoring.HitAccuracy,
					Scatter = colliderAuthoring.Scatter,
					LegacyMode = true, // todo colliderAuthoring.LegacyMode,
					ZLow = Surface?.Height(Position) ?? PlayfieldHeight
				});

				dstManager.AddComponentData(entity, new KickerCollisionData {
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
			transform.GetComponentInParent<Player>().RegisterKicker(this, entity, ParentEntity);
		}

		public override IEnumerable<MonoBehaviour> SetData(KickerData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector2();
			Orientation = data.Orientation > 180f ? data.Orientation - 360f : data.Orientation;
			Radius = data.Radius;
			KickerType = data.KickerType;

			// collider data
			var colliderAuthoring = gameObject.GetComponent<KickerColliderAuthoring>();
			if (colliderAuthoring) {
				colliderAuthoring.enabled = data.IsEnabled;

				colliderAuthoring.Scatter = data.Scatter;
				colliderAuthoring.HitAccuracy = data.HitAccuracy;
				colliderAuthoring.HitHeight = data.HitHeight;
				colliderAuthoring.FallThrough = data.FallThrough;
				colliderAuthoring.LegacyMode = data.LegacyMode;
				colliderAuthoring.EjectAngle = data.Angle;
				colliderAuthoring.EjectSpeed = data.Speed;

				updatedComponents.Add(colliderAuthoring);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(KickerData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);
			return Array.Empty<MonoBehaviour>();
		}

		public override KickerData CopyDataTo(KickerData data, string[] materialNames, string[] textureNames,
			bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2D();
			data.Orientation = Orientation;
			data.Radius = Radius;
			data.Surface = Surface != null ? Surface.name : string.Empty;

			// todo visibility is set by the type

			var colliderAuthoring = gameObject.GetComponent<KickerColliderAuthoring>();
			if (colliderAuthoring) {
				data.IsEnabled = colliderAuthoring.enabled;
				data.Scatter = colliderAuthoring.Scatter;
				data.HitAccuracy = colliderAuthoring.HitAccuracy;
				data.HitHeight = colliderAuthoring.HitHeight;
				data.FallThrough = colliderAuthoring.FallThrough;
				data.LegacyMode = colliderAuthoring.LegacyMode;
				data.Angle = colliderAuthoring.EjectAngle;
				data.Speed = colliderAuthoring.EjectSpeed;

			} else {
				data.IsEnabled = false;
			}

			return data;
		}

		#endregion

		#region IBallCreationPosition

		public Vertex3D GetBallCreationPosition() => new Vertex3D(Position.x, Position.y, PositionZ);

		public Vertex3D GetBallCreationVelocity() => new Vertex3D(0.1f, 0, 0);

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;

		public override Vector3 GetEditorPosition() => Position;

		public override void SetEditorPosition(Vector3 pos) => Position = ((float3)pos).xy;

		public override ItemDataTransformType EditorRotationType =>
			KickerType == Engine.VPT.KickerType.KickerCup || KickerType == Engine.VPT.KickerType.KickerWilliams
				? ItemDataTransformType.OneD : ItemDataTransformType.None;

		public override Vector3 GetEditorRotation() => new Vector3(Orientation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Orientation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override void SetEditorScale(Vector3 rot) => Radius = rot.x;
		public override Vector3 GetEditorScale() => new Vector3(Radius, 0f, 0f);

		#endregion
	}
}
