// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
#endregion

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Surface")]
	public class SurfaceAuthoring : ItemAuthoring<Surface, SurfaceData>, IHittableAuthoring, ISwitchableAuthoring, IConvertGameObjectToEntity, IDragPointsEditable
	{
		public override string DefaultDescription => "Surface";

		protected override string[] Children => new [] { "Side", "Top" };

		protected override Surface GetItem() => new Surface(data);

		public IHittable Hittable => Item;

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Surface>(Name);
			}
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			dstManager.AddComponentData(entity, new LineSlingshotData {
				IsDisabled = false,
				Threshold = data.SlingshotThreshold,
			});
			transform.GetComponentInParent<Player>().RegisterSurface(Item, entity, gameObject);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() {
			if (data == null || data.DragPoints.Length == 0) {
				return Vector3.zero;
			}
			return data.DragPoints[0].Center.ToUnityVector3();
		}
		public override void SetEditorPosition(Vector3 pos) {
			if (data == null || data.DragPoints.Length == 0) {
				return;
			}

			var diff = pos.ToVertex3D().Sub(data.DragPoints[0].Center);
			diff.Z = 0f;
			data.DragPoints[0].Center = pos.ToVertex3D();
			for (int i = 1; i < data.DragPoints.Length; i++) {
				var pt = data.DragPoints[i];
				pt.Center = pt.Center.Add(diff);
			}
		}

		//IDragPointsEditable
		public bool DragPointEditEnabled { get; set; }
		public DragPointData[] GetDragPoints() => data.DragPoints;
		public void SetDragPoints(DragPointData[] dragPoints) { data.DragPoints = dragPoints; }
		public Vector3 GetEditableOffset() => new Vector3(0.0f, 0.0f, data.HeightBottom);
		public Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public bool PointsAreLooping() => true;
		public IEnumerable<DragPointExposure> GetDragPointExposition() => new[] { DragPointExposure.Smooth , DragPointExposure.SlingShot , DragPointExposure.Texture };
		public ItemDataTransformType GetHandleType() => ItemDataTransformType.TwoD;
	}
}
