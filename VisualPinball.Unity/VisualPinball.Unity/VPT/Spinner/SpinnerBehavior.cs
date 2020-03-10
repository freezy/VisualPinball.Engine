#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Spinner;

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
	}
}
