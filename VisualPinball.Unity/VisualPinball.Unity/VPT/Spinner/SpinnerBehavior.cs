#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Spinner
{
	[AddComponentMenu("Visual Pinball/Spinner")]
	public class SpinnerBehavior : ItemBehavior<Engine.VPT.Spinner.Spinner, SpinnerData>
	{
		protected override string[] Children => new [] { "Plate", "Bracket" };

		protected override Engine.VPT.Spinner.Spinner GetItem()
		{
			return new Engine.VPT.Spinner.Spinner(data);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(data.Height);
		public override void SetEditorPosition(Vector3 pos)
		{
			data.Center = pos.ToVertex2Dxy();
			data.Height = pos.z;
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(data.Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => data.Rotation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(data.Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => data.Length = scale.x;
	}
}
