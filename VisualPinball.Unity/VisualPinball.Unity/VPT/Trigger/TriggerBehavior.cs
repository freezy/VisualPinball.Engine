#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Trigger
{
	[AddComponentMenu("Visual Pinball/Trigger")]
	public class TriggerBehavior : ItemBehavior<Engine.VPT.Trigger.Trigger, TriggerData>
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
	}
}
