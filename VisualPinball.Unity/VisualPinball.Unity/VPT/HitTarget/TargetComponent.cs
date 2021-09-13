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
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	public abstract class TargetComponent : ItemMainRenderableComponent<HitTargetData>,
		ISwitchDeviceComponent, ITargetData, IMeshGenerator
	{
		#region Data

		[Tooltip("Position of the target on the playfield.")]
		public Vector3 Position;

		[Range(-180f, 180f)]
		[Tooltip("Z-Axis rotation of the target.")]
		public float Rotation;

		[Tooltip("Overall scaling of the target.")]
		public Vector3 Size = new Vector3(32f, 32f, 32f);

		public int _targetType = Engine.VPT.TargetType.DropTargetBeveled;
		public string _meshName;

		#endregion

		#region IHitTargetData

		public bool IsLegacy {
			get {
				var colliderComponent = GetComponent<HitTargetColliderComponent>();
				return colliderComponent && colliderComponent.IsLegacy;
			}
		}

		public int TargetType => _targetType;

		public float RotZ => Rotation;
		public float ScaleX => Size.x;
		public float ScaleY => Size.y;
		public float ScaleZ => Size.z;
		public float PositionX => Position.x;
		public float PositionY => Position.y;
		public float PositionZ => Position.z;

		#endregion

		#region IMeshGenerator

		public Mesh GetMesh() => GetDefaultMesh();

		public Matrix3D GetTransformationMatrix()
		{
			var t = transform;
			return Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale).ToVpMatrix();
		}

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.HitTarget;
		public override string ItemName => "Target";

		public override IEnumerable<Type> ValidParents => HitTargetColliderComponent.ValidParentTypes
			.Distinct();

		public override HitTargetData InstantiateData() => new HitTargetData();
		protected override Type MeshComponentType { get; } = typeof(ItemMeshComponent<HitTargetData, TargetComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ItemColliderComponent<HitTargetData, TargetComponent>);

		public const string SwitchItem = "target_switch";

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem) { IsPulseSwitch = true }
		};
		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#endregion

		#region Transformation

		protected abstract float ZOffset { get; }

		public override void UpdateTransforms()
		{
			base.UpdateTransforms();

			var t = transform;
			t.localPosition = new Vector3(Position.x, Position.y, Position.z + PlayfieldHeight + ZOffset);
			t.localScale = Size;
			t.localEulerAngles = new Vector3(0, 0, Rotation);
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(HitTargetData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			// transforms
			Position = data.Position.ToUnityVector3();
			Rotation = data.RotZ > 180f ? data.RotZ - 360f : data.RotZ;
			Size = data.Size.ToUnityVector3();

			_targetType = data.TargetType;
			#if UNITY_EDITOR
			var mf = GetComponent<MeshFilter>();
			if (mf) {
				_meshName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mf.sharedMesh));
			}
			#endif

			return updatedComponents;
		}

		public override HitTargetData CopyDataTo(HitTargetData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			// name and transforms
			data.Name = name;
			data.Position = Position.ToVertex3D();
			data.RotZ = Rotation;
			data.Size = Size.ToVertex3D();

			data.TargetType = _targetType;
			data.IsVisible = GetEnabled<Renderer>();

			return data;
		}

		#endregion

		#region Editor Tooling

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => Position;
		public override void SetEditorPosition(Vector3 pos) => Position = pos;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Rotation = ClampDegrees(rot.x);

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale() => Size;
		public override void SetEditorScale(Vector3 scale) => Size = scale;

		#endregion
	}
}
