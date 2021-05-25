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

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity
{
	public class FlipperComponent : ItemMainRenderableComponent
	{
		#region Data

		public Vector2 Center;
		public float StartAngle;
		public float EndAngle;
		public float BaseRadius;
		public float EndRadius;
		public float FlipperRadius;
		public float Height;
		public float RubberHeight;
		public float RubberWidth;

		public void Set(Flipper flipper)
		{
			Center = flipper.Data.Center.ToUnityVector2();
			StartAngle = flipper.Data.StartAngle;
			EndAngle = flipper.Data.EndAngle;
			BaseRadius = flipper.Data.BaseRadius;
			EndRadius = flipper.Data.EndRadius;
			FlipperRadius = flipper.Data.FlipperRadius;
			Height = flipper.Data.Height;
			RubberHeight = flipper.Data.RubberHeight;
			RubberWidth = flipper.Data.RubberWidth;
		}

		#endregion

		#region Editor

		protected override IEnumerable<Type> MeshAuthoringTypes => new[] {typeof(FlipperBaseMeshComponent), typeof(FlipperRubberMeshComponent)};

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => Center;
		public override void SetEditorPosition(Vector3 pos) => Center = pos;

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(StartAngle, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => StartAngle = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;

		public override Vector3 GetEditorScale() => new Vector3(BaseRadius, FlipperRadius, Height);
		public override void SetEditorScale(Vector3 scale)
		{
			if (BaseRadius > 0) {
				var endRadiusRatio = EndRadius / BaseRadius;
				EndRadius = scale.x * endRadiusRatio;
			}
			BaseRadius = scale.x;
			FlipperRadius = scale.y;
			if (Height > 0) {
				var rubberHeightRatio = RubberHeight / Height;
				RubberHeight = scale.z * rubberHeightRatio;
				var rubberWidthRatio = RubberWidth / Height;
				RubberWidth = scale.z * rubberWidthRatio;
			}
			Height = scale.z;
		}

		protected void OnDrawGizmosSelected()
		{
			//base.OnDrawGizmosSelected();

			// draw end position mesh
			var mfs = GetComponentsInChildren<MeshFilter>();
			Gizmos.color = EndAngleMeshColor;
			Gizmos.matrix = Matrix4x4.identity;
			var baseRotation = math.normalize(math.mul(
				math.normalize(transform.rotation),
				quaternion.EulerXYZ(0, 0, -math.radians(StartAngle))
			));
			foreach (var mf in mfs) {
				var t = mf.transform;
				var r = math.mul(baseRotation, quaternion.EulerXYZ(0, 0, math.radians(EndAngle)));
				Gizmos.DrawWireMesh(mf.sharedMesh, t.position, r, t.lossyScale);
			}
		}

		private static readonly Color EndAngleMeshColor = new Color32(0, 255, 248, 10);

		#endregion
	}
}
