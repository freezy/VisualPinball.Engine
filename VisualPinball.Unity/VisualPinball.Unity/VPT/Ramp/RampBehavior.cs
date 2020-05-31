#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Ramp
{
	[AddComponentMenu("Visual Pinball/Ramp")]
	public class RampBehavior : ItemBehavior<Engine.VPT.Ramp.Ramp, RampData>
	{
		protected override string[] Children => new []{ "Floor", "RightWall", "LeftWall", "Wire1", "Wire2", "Wire3", "Wire4" };

		protected override Engine.VPT.Ramp.Ramp GetItem()
		{
			return new Engine.VPT.Ramp.Ramp(data);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition()
		{
			if (data == null || data.DragPoints.Length == 0) {
				return Vector3.zero;
			}
			return data.DragPoints[0].Vertex.ToUnityVector3();
		}
		public override void SetEditorPosition(Vector3 pos)
		{
			if (data == null || data.DragPoints.Length == 0) {
				return;
			}

			var diff = pos.ToVertex3D().Sub(data.DragPoints[0].Vertex);
			diff.Z = 0f;
			data.DragPoints[0].Vertex = pos.ToVertex3D();
			for (int i = 1; i < data.DragPoints.Length; i++) {
				var pt = data.DragPoints[i];
				pt.Vertex = pt.Vertex.Add(diff);
			}
		}
	}
}
