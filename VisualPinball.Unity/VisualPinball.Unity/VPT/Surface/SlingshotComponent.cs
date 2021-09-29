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

// ReSharper disable InconsistentNaming

using System;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	public class SlingshotComponent : MonoBehaviour, IMeshComponent, IMainRenderableComponent, IRubberData
	{
		public SurfaceColliderComponent SlingshotSurface;
		public RubberComponent RubberOn;
		public RubberComponent RubberOff;

		[NonSerialized] public float Position;
		[SerializeField] private bool _isLocked;

		#region IRubberData

		public DragPointData[] DragPoints => DragPointsAt(Position);
		public int Thickness => RubberOff.GetComponent<RubberComponent>()?.Thickness ?? 8;
		public float Height => RubberOff.GetComponent<RubberComponent>()?.Height ?? 25f;
		public float RotX => RubberOff.GetComponent<RubberComponent>()?.RotX ?? 0;
		public float RotY => RubberOff.GetComponent<RubberComponent>()?.RotY ?? 0;
		public float RotZ => RubberOff.GetComponent<RubberComponent>()?.RotZ ?? 0;

		#endregion

		#region IMeshComponent

		public IMainRenderableComponent MainRenderableComponent => this;

		public void RebuildMeshes()
		{
			var mf = GetComponent<MeshFilter>();
			if (!mf) {
				Debug.LogWarning("Mesh filter or renderer not found.");
				return;
			}

			if (!RubberOff) {
				return;
			}
			var pf = GetComponentInParent<PlayfieldComponent>();
			var r0 = RubberOff.GetComponent<RubberComponent>();
			if (!r0 || !pf) {
				return;
			}

			var mesh = new RubberMeshGenerator(this)
				.GetTransformedMesh(pf.PlayfieldHeight, r0.Height, pf.PlayfieldDetailLevel)
				.ToUnityMesh();

			mf.sharedMesh = mesh;
		}

		#endregion

		#region IMainRenderableComponent

		public bool IsLocked { get => _isLocked; set => _isLocked = value; }
		public bool CanBeTransformed => false;
		public string ItemName => "Slingshot";
		public Entity Entity { get; set; }

		public void UpdateTransforms() { }
		public void UpdateVisibility() { }

		public ItemDataTransformType EditorPositionType => ItemDataTransformType.None;
		public Vector3 GetEditorPosition() => Vector3.zero;
		public void SetEditorPosition(Vector3 pos) { }

		public ItemDataTransformType EditorRotationType => ItemDataTransformType.None;
		public Vector3 GetEditorRotation() => Vector3.zero;
		public void SetEditorRotation(Vector3 pos) { }

		public ItemDataTransformType EditorScaleType => ItemDataTransformType.None;
		public Vector3 GetEditorScale() => Vector3.one;
		public void SetEditorScale(Vector3 pos) { }

		#endregion

		private DragPointData[] DragPointsAt(float pos)
		{
			if (RubberOn == null || RubberOff == null) {
				Debug.LogWarning("Rubber references not set.");
				return Array.Empty<DragPointData>();
			}
			var r0 = RubberOff.GetComponent<RubberComponent>();
			var r1 = RubberOn.GetComponent<RubberComponent>();
			if (r0 == null || r1 == null || r0.DragPoints == null || r1.DragPoints == null) {
				Debug.LogWarning("Rubber references not found or drag points not set.");
				return Array.Empty<DragPointData>();
			}

			var dp0 = r0.DragPoints;
			var dp1 = r1.DragPoints;

			if (dp0.Length != dp1.Length) {
				Debug.LogWarning($"Drag point number varies ({dp0.Length} vs {dp1.Length}.).");
				return Array.Empty<DragPointData>();
			}

			var dp = new DragPointData[dp0.Length];
			for (var i = 0; i < dp.Length; i++) {
				dp[i] = dp0[i].Lerp(dp1[i], pos);
			}

			return dp;
		}
	}
}
