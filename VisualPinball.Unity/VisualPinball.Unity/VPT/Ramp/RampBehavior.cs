#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Ramp
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Ramp")]
	public class RampBehavior : ItemBehavior<Engine.VPT.Ramp.Ramp, RampData>, IDragPointsEditable, IConvertGameObjectToEntity
	{
		protected override string[] Children => new[] { "Floor", "RightWall", "LeftWall", "Wire1", "Wire2", "Wire3", "Wire4" };

		protected override Engine.VPT.Ramp.Ramp GetItem() => new Engine.VPT.Ramp.Ramp(data);

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// register
			transform.GetComponentInParent<Player>().RegisterRamp(Item, entity, gameObject);
		}

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				_table.Ramps.Remove(Name);
			}
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition()
		{
			if (data == null || data.DragPoints.Length == 0) {
				return Vector3.zero;
			}
			return data.DragPoints[0].Center.ToUnityVector3();
		}
		public override void SetEditorPosition(Vector3 pos)
		{
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
		public Vector3 GetDragPointOffset(float ratio) => new Vector3(0.0f, 0.0f, (data.HeightTop - data.HeightBottom) * ratio);
		public bool PointsAreLooping() => false;
		public IEnumerable<DragPointExposure> GetDragPointExposition() => new DragPointExposure[] { DragPointExposure.Smooth, DragPointExposure.SlingShot };
		public ItemDataTransformType GetHandleType() => ItemDataTransformType.ThreeD;
	}
}
