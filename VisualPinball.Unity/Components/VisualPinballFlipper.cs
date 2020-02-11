#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballFlipper : ItemComponent<Flipper, FlipperData>
	{
		protected override string[] Children => new []{"Base", "Rubber"};

		protected override Flipper GetItem()
		{
			return new Flipper(data);
		}
	}
}
