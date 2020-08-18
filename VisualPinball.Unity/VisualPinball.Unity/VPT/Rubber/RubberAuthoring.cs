#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Rubber")]
	public class RubberAuthoring : ItemAuthoring<Rubber, RubberData>, IDragPointsEditable, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		protected override Rubber GetItem() => new Rubber(data);

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

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition()
		{
			if (data == null || data.DragPoints.Length == 0) {
				return Vector3.zero;
			}
			return data.DragPoints[0].Center.ToUnityVector3(data.Height);
		}
		public override void SetEditorPosition(Vector3 pos)
		{
			if (data == null || data.DragPoints.Length == 0) {
				return;
			}

			data.Height = pos.z;
			pos.z = 0f;
			var diff = pos.ToVertex3D().Sub(data.DragPoints[0].Center);
			diff.Z = 0f;
			data.DragPoints[0].Center = pos.ToVertex3D();
			for (int i = 1; i < data.DragPoints.Length; i++) {
				var pt = data.DragPoints[i];
				pt.Center = pt.Center.Add(diff);
			}
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorRotation() => new Vector3(data.RotX, data.RotY, data.RotZ);
		public override void SetEditorRotation(Vector3 rot)
		{
			data.RotX = rot.x;
			data.RotY = rot.y;
			data.RotZ = rot.z;
		}

		//IDragPointsEditable
		public bool DragPointEditEnabled { get; set; }
		public DragPointData[] GetDragPoints() => data.DragPoints;
		public void SetDragPoints(DragPointData[] dragPoints) { data.DragPoints = dragPoints; }
		public Vector3 GetEditableOffset() => new Vector3(0.0f, 0.0f, data.HitHeight);
		public Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public bool PointsAreLooping() => true;
		public IEnumerable<DragPointExposure> GetDragPointExposition() => new[] { DragPointExposure.Smooth };
		public ItemDataTransformType GetHandleType() => ItemDataTransformType.TwoD;
	}
}
