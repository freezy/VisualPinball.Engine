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
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Trigger")]
	public class TriggerAuthoring : ItemAuthoring<Trigger, TriggerData>, IHittableAuthoring, ISwitchableAuthoring, IDragPointsEditable, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		protected override Trigger GetItem() => new Trigger(data);

		public IHittable Hittable => Item;

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Trigger>(Name);
			}
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			dstManager.AddComponentData(entity, new TriggerAnimationData());
			dstManager.AddComponentData(entity, new TriggerMovementData());
			dstManager.AddComponentData(entity, new TriggerStaticData {
				AnimSpeed = data.AnimSpeed,
				Radius = data.Radius,
				Shape = data.Shape,
				TableScaleZ = table.GetScaleZ()
			});

			// register
			var trigger = GetComponent<TriggerAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterTrigger(trigger, entity, gameObject);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;

		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos)
		{
			if (data == null || data.DragPoints.Length == 0) {
				return;
			}
			var diff = pos.ToVertex3D().Sub(data.Center);
			foreach (var pt in data.DragPoints) {
				pt.Center = pt.Center.Add(new Vertex3D(diff.X, diff.Y, 0f));
			}
			data.Center = pos.ToVertex2Dxy();
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(data.Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => data.Rotation = rot.x;

		//IDragPointsEditable
		public bool DragPointEditEnabled { get; set; }
		public DragPointData[] GetDragPoints() => data.DragPoints;
		public void SetDragPoints(DragPointData[] dragPoints) { data.DragPoints = dragPoints; }
		public Vector3 GetEditableOffset() => new Vector3(-data.Center.X, -data.Center.Y, 0.0f);
		public Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public bool PointsAreLooping() => true;
		public IEnumerable<DragPointExposure> GetDragPointExposition() => new[] { DragPointExposure.Smooth, DragPointExposure.SlingShot };
		public ItemDataTransformType GetHandleType() => ItemDataTransformType.TwoD;
	}
}
