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
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	public class LightInsertMeshComponent : MeshComponent<LightData, LightComponent>,
		IDragPointSplineOwner
	{
		#region Data

		public float InsertHeight = 20f;

		public float PositionZ = 0.1f;

		[SerializeField, HideInInspector]
		private DragPointData[] _dragPoints;

		[SerializeField]
		private DragPointSplineComponent _dragPointSpline;

		public DragPointData[] DragPoints {
			get => GetOrCreateDragPointSpline().DragPoints;
			set {
				if (!_dragPointSpline) {
					_dragPoints = value;
					GetOrCreateDragPointSpline();
				} else {
					_dragPointSpline.SetDragPoints(value);
				}
			}
		}
		public DragPointSplineComponent DragPointSpline => GetOrCreateDragPointSpline();

		#endregion

		protected override Mesh GetMesh(LightData data)
		{
			var playfieldComponent = GetComponentInParent<PlayfieldComponent>();
			var meshGen = new SurfaceMeshGenerator(new LightInsertData(DragPoints, InsertHeight), MainComponent.transform.position.TranslateToVpx().ToVertex3D());
			var topMesh = meshGen.GetMesh(SurfaceMeshGenerator.Top, playfieldComponent.Width, playfieldComponent.Height, 0, false);
			var sideMesh = meshGen.GetMesh(SurfaceMeshGenerator.Side, playfieldComponent.Width, playfieldComponent.Height, 0, false);
			return topMesh.Merge(sideMesh).TransformToWorld();
		}

		protected override PbrMaterial GetMaterial(LightData data, Table table)
		{
			var mat = table.GetMaterial(table.Data.PlayfieldMaterial);
			if (mat != null) {
				mat.Name += " (Playfield Insert)";
				return new PbrMaterial(mat, table.GetTexture(table.Data.Image));
			}

			mat = new Engine.VPT.Material("Playfield Insert");
			return new PbrMaterial(mat, table.GetTexture(table.Data.Image)) { DiffusionProfile = DiffusionProfileTemplate.Plastics };
		}

		private DragPointSplineComponent GetOrCreateDragPointSpline()
		{
			if (!_dragPointSpline) {
				_dragPointSpline = DragPointSplineComponent.Create(this,
					_dragPoints ?? Array.Empty<DragPointData>());
				_dragPoints = null;
			} else {
				_dragPointSpline.Bind(this);
			}
			return _dragPointSpline;
		}

		MonoBehaviour IDragPointSplineOwner.SplineOwner => this;
		Transform IDragPointSplineOwner.SplineTransform => MainComponent.transform;
		DragPointSplineComponent IDragPointSplineOwner.SplineComponent => DragPointSpline;
		bool IDragPointSplineOwner.SplineClosed => true;
		bool IDragPointSplineOwner.SplinePlanar => true;
		void IDragPointSplineOwner.ApplySplineConstraints(Spline spline, int knotIndex,
			SplineModification modification, IReadOnlyList<float3> previousPositions) { }
		void IDragPointSplineOwner.RebuildSplineMeshes() => MainComponent.RebuildMeshes();
	}
}
