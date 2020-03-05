#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballBumper : ItemComponent<Bumper, BumperData>
	{
		protected override string[] Children => new []{"Base", "Cap", "Ring", "Skirt"};

		protected override Bumper GetItem()
		{
			return new Bumper(data);
		}
	}
}
