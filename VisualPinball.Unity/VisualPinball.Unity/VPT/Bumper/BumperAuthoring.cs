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
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Bumper")]
	public class BumperAuthoring : ItemMainRenderableAuthoring<BumperData>,
		ISwitchDeviceAuthoring, ICoilDeviceAuthoring, IOnSurfaceAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Position of the bumper on the playfield.")]
		public Vector2 Position;

		[Range(20f, 250f)]
		[Tooltip("Radius of the bumper. Updates xy scaling. 50 = Original size.")]
		public float Radius = 45f;

		[Range(50f, 300f)]
		[Tooltip("Height of the bumper. Updates z scaling. 100 = Original size.")]
		public float HeightScale = 45f;

		[Range(0f, 360f)]
		[Tooltip("Orientation angle. Updates z rotation.")]
		public float Orientation;

		public ISurfaceAuthoring Surface { get => _surface as ISurfaceAuthoring; set => _surface = value as MonoBehaviour; }

		[SerializeField]
		[TypeRestriction(typeof(ISurfaceAuthoring), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this bumper is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Bumper;
		public override string ItemName => "Bumper";
		public override IEnumerable<Type> ValidParents => BumperColliderAuthoring.ValidParentTypes;

		public override BumperData InstantiateData() => new BumperData();
		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<BumperData, BumperAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<BumperData, BumperAuthoring>);

		private const string SkirtMeshName = "Bumper (Skirt)";
		private const string BaseMeshName = "Bumper (Base)";
		private const string CapMeshName = "Bumper (Cap)";
		private const string RingMeshName = "Bumper (Ring)";
		private const float PrefabMeshScale = 100f;

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(name) {
				Description = "Socket Switch",
				IsPulseSwitch = true,
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		public IEnumerable<GamelogicEngineCoil> AvailableCoils =>  new[] {
			new GamelogicEngineCoil(name) {
				Description = "Ring Coil"
			}
		};

		#endregion

		#region Transformation

		public void OnSurfaceUpdated() => UpdateTransforms();

		public float PositionZ => SurfaceHeight(Surface, Position);

		public override void UpdateTransforms()
		{
			var t = transform;

			// position
			t.localPosition = new Vector3(Position.x, Position.y, PositionZ);

			// scale
			t.localScale = new Vector3(Radius * 2f, Radius * 2f, HeightScale) / PrefabMeshScale;

			// rotation
			t.localEulerAngles = new Vector3(0, 0, Orientation);
		}

		#endregion

		#region Convertion

			public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// physics collision data
			var collComponent = GetComponentInChildren<BumperColliderAuthoring>();
			if (collComponent) {
				dstManager.AddComponentData(entity, new BumperStaticData {
					Force = collComponent.Force,
					HitEvent = collComponent.HitEvent,
					Threshold = collComponent.Threshold
				});

				// skirt animation data
				if (GetComponentInChildren<BumperSkirtAnimationAuthoring>()) {
					dstManager.AddComponentData(entity, new BumperSkirtAnimationData {
						BallPosition = default,
						AnimationCounter = 0f,
						DoAnimate = false,
						DoUpdate = false,
						EnableAnimation = true,
						Rotation = new float2(0, 0),
						HitEvent = collComponent.HitEvent,
						Center = Position
					});
				}
			}

			// ring animation data
			var ringAnimComponent = GetComponentInChildren<BumperRingAnimationAuthoring>();
			if (ringAnimComponent) {
				dstManager.AddComponentData(entity, new BumperRingAnimationData {

					// dynamic
					IsHit = false,
					Offset = 0,
					AnimateDown = false,
					DoAnimate = false,

					// static
					DropOffset = ringAnimComponent.RingDropOffset,
					HeightScale = HeightScale,
					Speed = ringAnimComponent.RingSpeed,
					ScaleZ = 1f
				});
			}

			// register at player
			GetComponentInParent<Player>().RegisterBumper(this, entity, ParentEntity);
		}

		public override IEnumerable<MonoBehaviour> SetData(BumperData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityFloat2();
			Radius = data.Radius;
			HeightScale = data.HeightScale;
			Orientation = data.Orientation;

			// collider
			var collComponent = GetComponentInChildren<BumperColliderAuthoring>();
			if (collComponent) {
				collComponent.enabled = data.IsCollidable;
				collComponent.Threshold = data.Threshold;
				collComponent.Force = data.Force;
				collComponent.Scatter = data.Scatter;
				collComponent.HitEvent = data.HitEvent;
			}

			// ring animation
			var ringAnimComponent = GetComponentInChildren<BumperRingAnimationAuthoring>();
			if (ringAnimComponent) {
				ringAnimComponent.RingSpeed = data.RingSpeed;
				ringAnimComponent.RingDropOffset = data.RingDropOffset;
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(BumperData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			Surface = GetAuthoring<SurfaceAuthoring>(components, data.Surface);
			UpdateTransforms();

			// children visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				var mr = mf.GetComponent<MeshRenderer>();
				switch (mf.sharedMesh.name) {
					case SkirtMeshName:
						mf.gameObject.SetActive(data.IsSocketVisible);
						if (!string.IsNullOrEmpty(data.SocketMaterial)) {
							mr.sharedMaterial = materialProvider.MergeMaterials(data.SocketMaterial, mr.sharedMaterial);
						}
						break;
					case BaseMeshName:
						mf.gameObject.SetActive(data.IsBaseVisible);
						if (!string.IsNullOrEmpty(data.BaseMaterial)) {
							mr.sharedMaterial = materialProvider.MergeMaterials(data.BaseMaterial, mr.sharedMaterial);
						}
						break;
					case CapMeshName:
						mf.gameObject.SetActive(data.IsCapVisible);
						if (!string.IsNullOrEmpty(data.CapMaterial)) {
							mr.sharedMaterial = materialProvider.MergeMaterials(data.CapMaterial, mr.sharedMaterial);
						}
						break;
					case RingMeshName:
						mf.gameObject.SetActive(data.IsRingVisible);
						if (!string.IsNullOrEmpty(data.RingMaterial)) {
							mr.sharedMaterial = materialProvider.MergeMaterials(data.RingMaterial, mr.sharedMaterial);
						}
						break;
				}
			}

			return Array.Empty<MonoBehaviour>();
		}


		public override BumperData CopyDataTo(BumperData data, string[] materialNames, string[] textureNames)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2D();
			data.Radius = Radius;
			data.HeightScale = HeightScale;
			data.Orientation = Orientation;

			// surface
			data.Surface = Surface != null ? Surface.name : string.Empty;

			// children visibility
			data.IsBaseVisible = false;
			data.IsCapVisible = false;
			data.IsRingVisible = false;
			data.IsSocketVisible = false;
			foreach (var mf in GetComponentsInChildren<MeshFilter>(true)) {
				var mr = mf.gameObject.GetComponent<MeshRenderer>();
				switch (mf.sharedMesh.name) {
					case SkirtMeshName:
						data.IsSocketVisible = mf.gameObject.activeInHierarchy;
						CopyMaterialName(mr, materialNames, textureNames, ref data.SocketMaterial);
						break;
					case BaseMeshName:
						data.IsBaseVisible = mf.gameObject.activeInHierarchy;
						CopyMaterialName(mr, materialNames, textureNames, ref data.BaseMaterial);
						break;
					case CapMeshName:
						data.IsCapVisible = mf.gameObject.activeInHierarchy;
						CopyMaterialName(mr, materialNames, textureNames, ref data.CapMaterial);
						break;
					case RingMeshName:
						data.IsRingVisible = mf.gameObject.activeInHierarchy;
						CopyMaterialName(mr, materialNames, textureNames, ref data.RingMaterial);
						break;
				}
			}

			// collider
			var collComponent = GetComponentInChildren<BumperColliderAuthoring>();
			if (collComponent) {
				data.IsCollidable = collComponent.enabled;
				data.Threshold = collComponent.Threshold;
				data.Force = collComponent.Force;
				data.Scatter = collComponent.Scatter;
				data.HitEvent = collComponent.HitEvent;
			} else {
				data.IsCollidable = false;
			}

			// ring animation
			var ringAnimComponent = GetComponentInChildren<BumperRingAnimationAuthoring>();
			if (ringAnimComponent) {
				data.RingSpeed = ringAnimComponent.RingSpeed;
				data.RingDropOffset = ringAnimComponent.RingDropOffset;
			}

			return data;
		}

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Surface != null
			? new Vector3(Position.x, Position.y, Surface.Height(Position))
			: new Vector3(Position.x, Position.y, 0);
		public override void SetEditorPosition(Vector3 pos) => Position = ((float3)pos).xy;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Orientation, 0, 0);
		public override void SetEditorRotation(Vector3 rot) => Orientation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Radius * 2f, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => Radius = scale.x / 2f;

		#endregion
	}
}
