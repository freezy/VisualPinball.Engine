#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballSpinner : ItemComponent<Spinner, SpinnerData>
	{
		protected override string[] Children => new [] { "Plate", "Bracket" };

		protected override Spinner GetItem()
		{
			return new Spinner(data);
		}
	}
}
