#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Trigger
{
	[AddComponentMenu("Visual Pinball/Trigger")]
	public class TriggerBehavior : ItemBehavior<Engine.VPT.Trigger.Trigger, TriggerData>, IDragPointsEditable
	{
		protected override string[] Children => null;

		protected override Engine.VPT.Trigger.Trigger GetItem()
		{
			return new Engine.VPT.Trigger.Trigger(data);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(data.Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => data.Rotation = rot.x;

		//IDragPointsEditable
		public bool DragPointEditEnabled { get; set; }
		public DragPointData[] GetDragPoints() { return data.DragPoints; }
		public void SetDragPoints(DragPointData[] dpoints) { data.DragPoints = dpoints; }
		public Vector3 GetEditableOffset() { return new Vector3(-data.Center.X, -data.Center.Y, 0.0f); }
		public Vector3 GetDragPointOffset(float ratio) { return Vector3.zero; }
		public bool PointsAreLooping() { return true; }
	}
}
