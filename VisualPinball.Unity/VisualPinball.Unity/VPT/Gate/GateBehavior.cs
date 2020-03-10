#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity.VPT.Gate
{
	[AddComponentMenu("Visual Pinball/Gate")]
	public class GateBehavior : ItemBehavior<Engine.VPT.Gate.Gate, GateData>
	{
		protected override string[] Children => new []{"Wire", "Bracket"};

		protected override Engine.VPT.Gate.Gate GetItem()
		{
			return new Engine.VPT.Gate.Gate(data);
		}
	}
}
