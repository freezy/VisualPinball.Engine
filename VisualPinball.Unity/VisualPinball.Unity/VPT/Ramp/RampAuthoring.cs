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

using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Ramp")]
	public class RampAuthoring : ItemMainAuthoring<Ramp, RampData>, IDragPointsEditable, IConvertGameObjectToEntity, IHittableAuthoring
	{
		protected override Ramp InstantiateItem(RampData data) => new Ramp(data);

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Ramp, RampData, RampAuthoring>);

		public IHittable Hittable => Item;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// register
			transform.GetComponentInParent<Player>().RegisterRamp(Item, entity, gameObject);
		}

		public void RemoveHittableComponent()
		{
		}

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Ramp>(Name);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition()
		{
			if (Data == null || Data.DragPoints.Length == 0) {
				return Vector3.zero;
			}
			return Data.DragPoints[0].Center.ToUnityVector3();
		}
		public override void SetEditorPosition(Vector3 pos)
		{
			if (Data == null || Data.DragPoints.Length == 0) {
				return;
			}

			var diff = pos.ToVertex3D().Sub(Data.DragPoints[0].Center);
			diff.Z = 0f;
			Data.DragPoints[0].Center = pos.ToVertex3D();
			for (int i = 1; i < Data.DragPoints.Length; i++) {
				var pt = Data.DragPoints[i];
				pt.Center = pt.Center.Add(diff);
			}
		}

		//IDragPointsEditable
		public bool DragPointEditEnabled { get; set; }
		public DragPointData[] GetDragPoints() => Data.DragPoints;
		public void SetDragPoints(DragPointData[] dragPoints) { Data.DragPoints = dragPoints; }
		public Vector3 GetEditableOffset() => new Vector3(0.0f, 0.0f, Data.HeightBottom);
		public Vector3 GetDragPointOffset(float ratio) => new Vector3(0.0f, 0.0f, (Data.HeightTop - Data.HeightBottom) * ratio);
		public bool PointsAreLooping() => false;
		public IEnumerable<DragPointExposure> GetDragPointExposition() => new DragPointExposure[] { DragPointExposure.Smooth, DragPointExposure.SlingShot };
		public ItemDataTransformType GetHandleType() => ItemDataTransformType.ThreeD;
	}
}
