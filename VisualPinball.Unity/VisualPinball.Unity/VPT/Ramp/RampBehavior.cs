#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity.VPT.Ramp
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Ramp")]
	public class RampBehavior : ItemBehavior<Engine.VPT.Ramp.Ramp, RampData>
	{
		protected override string[] Children => new []{ "Floor", "RightWall", "LeftWall", "Wire1", "Wire2", "Wire3", "Wire4" };

		protected override Engine.VPT.Ramp.Ramp GetItem()
		{
			return new Engine.VPT.Ramp.Ramp(data);
		}
	}
}
