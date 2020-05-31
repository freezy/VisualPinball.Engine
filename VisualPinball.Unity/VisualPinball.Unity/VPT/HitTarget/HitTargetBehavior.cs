#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.HitTarget
{
	[AddComponentMenu("Visual Pinball/Hit Target")]
	public class HitTargetBehavior : ItemBehavior<Engine.VPT.HitTarget.HitTarget, HitTargetData>
	{
		protected override string[] Children => null;

		protected override Engine.VPT.HitTarget.HitTarget GetItem()
		{
			return new Engine.VPT.HitTarget.HitTarget(data);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition()
		{
			return data.Position.ToUnityVector3();
		}
		public override void SetEditorPosition(Vector3 pos)
		{
			data.Position = pos.ToVertex3D();
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation()
		{
			return new Vector3(data.RotZ, 0f, 0f);
		}
		public override void SetEditorRotation(Vector3 rot)
		{
			data.RotZ = rot.x;
		}

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale()
		{
			return data.Size.ToUnityVector3();
		}
		public override void SetEditorScale(Vector3 scale)
		{
			data.Size = scale.ToVertex3D();
		}
	}
}
