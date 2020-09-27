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
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Rubber")]
	public class RubberAuthoring : ItemAuthoring<Rubber, RubberData>, IDragPointsEditable, IHittableAuthoring, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		protected override Rubber InstantiateItem(RubberData data) => new Rubber(data);

		public IHittable Hittable => Item;

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Rubber>(Name);
			}
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// register
			transform.GetComponentInParent<Player>().RegisterRubber(Item, entity, gameObject);
		}

		public void RemoveHittableComponent()
		{
			var hc = gameObject.GetComponent<RubberColliderAuthoring>();
			if (hc != null) {
				DestroyImmediate(hc);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition()
		{
			if (Data == null || Data.DragPoints.Length == 0) {
				return Vector3.zero;
			}
			return Data.DragPoints[0].Center.ToUnityVector3(Data.Height);
		}
		public override void SetEditorPosition(Vector3 pos)
		{
			SetEditorPositionChildren(pos);
			if (Data == null || Data.DragPoints.Length == 0) {
				return;
			}

			Data.Height = pos.z;
			pos.z = 0f;
			var diff = pos.ToVertex3D().Sub(Data.DragPoints[0].Center);
			diff.Z = 0f;
			Data.DragPoints[0].Center = pos.ToVertex3D();
			for (var i = 1; i < Data.DragPoints.Length; i++) {
				var pt = Data.DragPoints[i];
				pt.Center = pt.Center.Add(diff);
			}
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorRotation() => new Vector3(Data.RotX, Data.RotY, Data.RotZ);
		public override void SetEditorRotation(Vector3 rot)
		{
			Data.RotX = rot.x;
			Data.RotY = rot.y;
			Data.RotZ = rot.z;
		}

		//IDragPointsEditable
		public bool DragPointEditEnabled { get; set; }
		public DragPointData[] GetDragPoints() => Data.DragPoints;
		public void SetDragPoints(DragPointData[] dragPoints) { Data.DragPoints = dragPoints; }
		public Vector3 GetEditableOffset() => new Vector3(0.0f, 0.0f, Data.HitHeight);
		public Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public bool PointsAreLooping() => true;
		public IEnumerable<DragPointExposure> GetDragPointExposition() => new[] { DragPointExposure.Smooth };
		public ItemDataTransformType GetHandleType() => ItemDataTransformType.TwoD;
	}
}
