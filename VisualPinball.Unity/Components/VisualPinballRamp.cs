#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballRamp : ItemComponent<Ramp, RampData>
	{
		protected override string[] Children => new []{ "Floor", "RightWall", "LeftWall", "Wire1", "Wire2", "Wire3", "Wire4" };

		protected override Ramp GetItem()
		{
			return new Ramp(data);
		}
	}
}
