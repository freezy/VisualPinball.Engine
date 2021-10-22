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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Gate")]
	public class GateComponent : MainRenderableComponent<GateData>,
		IGateData, ISwitchDeviceComponent, IOnSurfaceComponent, IConvertGameObjectToEntity
	{
		#region Data

		[Tooltip("Position of the gate on the playfield.")]
		public Vector3 Position;

		[Range(-180f, 180f)]
		[Tooltip("Angle of the gate on the playfield (z-axis rotation)")]
		public float _rotation;

		[Range(10f, 250f)]
		[Tooltip("How much the gate is scaled, in percent.")]
		public float _length = 100f;

		public ISurfaceComponent Surface { get => _surface as ISurfaceComponent; set => _surface = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(ISurfaceComponent), PickerLabel = "Walls & Ramps", UpdateTransforms = true)]
		[Tooltip("On which surface this flipper is attached to. Updates Z-translation.")]
		public MonoBehaviour _surface;

		public int _type;
		public string _meshName;

		#endregion

		#region IGateData

		public float PosX => Position.x;
		public float PosY => Position.y;
		public float Height => Position.z;

		public float Rotation => _rotation;
		public float Length => _length;

		public bool ShowBracket { get {
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.gameObject.name) {
					case BracketObjectName:
						return mf.gameObject.activeInHierarchy;
				}
			}
			return false;
		}}

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Gate;
		public override string ItemName => "Gate";

		public override IEnumerable<Type> ValidParents => GateColliderComponent.ValidParentTypes
			.Distinct();

		public override GateData InstantiateData() => new GateData();

		protected override Type MeshComponentType { get; } = typeof(MeshComponent<GateData, GateComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<GateData, GateComponent>);

		public const string BracketObjectName = "Bracket";
		public const string WireObjectName = "Wire";

		public const string MainSwitchItem = "gate_switch";

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(MainSwitchItem)  {
				IsPulseSwitch = true
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#endregion

		#region Transformation

		public void OnSurfaceUpdated() => UpdateTransforms();

		public float PositionZ => SurfaceHeight(Surface, Position);

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();
			var t = transform;

			// position
			t.localPosition = new Vector3(Position.x, Position.y, Position.z + PositionZ);

			// scale
			t.localScale = new Vector3(Length, Length, Length);

			// rotation
			t.localEulerAngles = new Vector3(0, 0, Rotation);
		}

		#endregion

		#region Conversion

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// collision
			var colliderComponent = gameObject.GetComponent<GateColliderComponent>();
			if (colliderComponent) {

				dstManager.AddComponentData(entity, new GateStaticData {
					AngleMin = math.radians(colliderComponent._angleMin),
					AngleMax = math.radians(colliderComponent._angleMax),
					Height = Position.z,
					Damping = math.pow(colliderComponent.Damping, (float)PhysicsConstants.PhysFactor),
					GravityFactor = colliderComponent.GravityFactor,
					TwoWay = colliderComponent.TwoWay,
				});

				// movement data
				if (GetComponentInChildren<GateWireAnimationComponent>()) {
					dstManager.AddComponentData(entity, new GateMovementData {
						Angle = math.radians(colliderComponent._angleMin),
						AngleSpeed = 0,
						ForcedMove = false,
						IsOpen = false,
						HitDirection = false
					});
				}
			}

			// register
			transform.GetComponentInParent<Player>().RegisterGate(this, entity, ParentEntity);
		}

		public override IEnumerable<MonoBehaviour> SetData(GateData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Center.ToUnityVector3(data.Height);
			_rotation = data.Rotation > 180f ? data.Rotation - 360f : data.Rotation;
			_length = data.Length;
			_type = data.GateType;

			// collider data
			var colliderComponent = gameObject.GetComponent<GateColliderComponent>();
			if (colliderComponent) {
				colliderComponent._angleMin = math.degrees(data.AngleMin);
				colliderComponent._angleMax = math.degrees(data.AngleMax);
				if (colliderComponent._angleMin > 180f) {
					colliderComponent._angleMin -= 360f;
				}
				if (colliderComponent._angleMax > 180f) {
					colliderComponent._angleMax -= 360f;
				}
				colliderComponent.Damping = data.Damping;
				colliderComponent.Elasticity = data.Elasticity;
				colliderComponent.Friction = data.Friction;
				colliderComponent.GravityFactor = data.GravityFactor;
				colliderComponent._twoWay = data.TwoWay;

				updatedComponents.Add(colliderComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(GateData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			Surface = FindComponent<ISurfaceComponent>(components, data.Surface);

			// visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.gameObject.name) {
					case BracketObjectName:
						mf.gameObject.SetActive(data.IsVisible && data.ShowBracket);
						break;
					case WireObjectName:
						#if UNITY_EDITOR
						_meshName = System.IO.Path.GetFileNameWithoutExtension(UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh));
						#endif
						mf.gameObject.SetActive(data.IsVisible);
						break;
					default:
						mf.gameObject.SetActive(data.IsVisible);
						break;
				}
			}

			return Array.Empty<MonoBehaviour>();
		}

		public override GateData CopyDataTo(GateData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Center = Position.ToVertex2Dxy();
			data.Rotation = Rotation;
			data.Height = Position.z;
			data.Length = Length;
			data.Surface = Surface != null ? Surface.name : string.Empty;

			data.GateType = _type;

			// visibility
			foreach (var mf in GetComponentsInChildren<MeshFilter>()) {
				switch (mf.gameObject.name) {
					case BracketObjectName:
						data.ShowBracket = mf.gameObject.activeInHierarchy;
						break;
					case WireObjectName:
						data.IsVisible = mf.gameObject.activeInHierarchy;
						break;
				}
			}

			// collision data
			var colliderComponent = gameObject.GetComponent<GateColliderComponent>();
			if (colliderComponent) {
				data.IsCollidable = colliderComponent.enabled;

				data.AngleMin = math.radians(colliderComponent._angleMin);
				data.AngleMax = math.radians(colliderComponent._angleMax);
				data.Damping = colliderComponent.Damping;
				data.Elasticity = colliderComponent.Elasticity;
				data.Friction = colliderComponent.Friction;
				data.GravityFactor = colliderComponent.GravityFactor;
				data.TwoWay = colliderComponent._twoWay;

			} else {
				data.IsCollidable = false;
			}

			return data;
		}

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override void SetEditorPosition(Vector3 pos) => Position = pos;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => _rotation = ClampDegrees(rot.x);

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => _length = scale.x;

		#endregion
	}
}
