using UnityEngine;

namespace VisualPinball.Unity
{
	public static class TransformExtensions
	{
		public static void SetFromMatrix(this Transform tf, Matrix4x4 trs)
		{
			tf.localScale = new Vector3(
				trs.GetColumn(0).magnitude,
				trs.GetColumn(1).magnitude,
				trs.GetColumn(2).magnitude
			);
			tf.localPosition = trs.GetColumn(3);
			tf.localRotation = Quaternion.LookRotation(
				trs.GetColumn(2),
				trs.GetColumn(1)
			);
		}
	}
}
