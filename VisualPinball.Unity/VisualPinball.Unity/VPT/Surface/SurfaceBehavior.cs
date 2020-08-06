#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.VPT.Surface
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Surface")]
	public class SurfaceBehavior : ItemBehavior<Engine.VPT.Surface.Surface, SurfaceData>, IConvertGameObjectToEntity, IDragPointsEditable
	{
		protected override string[] Children => new [] { "Side", "Top" };

		protected override Engine.VPT.Surface.Surface GetItem() => new Engine.VPT.Surface.Surface(data);

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table.Remove<Engine.VPT.Surface.Surface>(Name);
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
