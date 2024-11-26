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
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	public abstract class TargetComponent : MainRenderableComponent<HitTargetData>,
		ISwitchDeviceComponent, ITargetData, IMeshGenerator
	{
		#region Data

		public Vector3 Position {
			get => transform.localPosition.TranslateToVpx();
			set => transform.localPosition = value.TranslateToWorld();
		}

		public float Rotation {
			get => transform.localEulerAngles.y > 180 ? transform.localEulerAngles.y - 360 : transform.localEulerAngles.y;
			set => transform.SetLocalYRotation(math.radians(value));
		}

		public float3 Size {
			get => transform.localScale * 32f;
			set => transform.localScale = value / 32f;
		}

		public int _targetType = Engine.VPT.TargetType.DropTargetBeveled;
		public string _meshName;

		#endregion

		#region IHitTargetData

		public virtual bool IsLegacy => false;

		public int TargetType => _targetType;

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

		public override HitTargetData InstantiateData() => new HitTargetData();
		public override bool HasProceduralMesh => false;
		protected override Type MeshComponentType { get; } = typeof(MeshComponent<HitTargetData, TargetComponent>);
		protected override Type ColliderComponentType { get; } = typeof(ColliderComponent<HitTargetData, TargetComponent>);

		public const string SwitchItem = "target_switch";

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem) { IsPulseSwitch = !(this is DropTargetComponent) }
		};
		public SwitchDefault SwitchDefault => SwitchDefault.Configurable;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#endregion

		#region Transformation

		protected abstract float ZOffset { get; }

		public float4x4 TransformationWithinPlayfield => transform.worldToLocalMatrix.WorldToLocalTranslateWithinPlayfield(Playfield.transform.localToWorldMatrix);

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
			data.Size = ((Vector3)Size).ToVertex3D();

			data.TargetType = _targetType;
			data.IsVisible = GetEnabled<Renderer>();

			return data;
		}

		public override void CopyFromObject(GameObject go)
		{
			var targetComponent = go.GetComponent<TargetComponent>();
			if (targetComponent != null) {
				Position = targetComponent.Position;
				Size = targetComponent.Size;
				Rotation = targetComponent.Rotation;

			} else {
				Position = go.transform.localPosition.TranslateToVpx();
				Size = go.transform.localScale;
				Rotation = go.transform.localEulerAngles.z;
			}

			UpdateTransforms();
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
