#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity.VPT.Bumper
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Bumper")]
	public class BumperBehavior : ItemBehavior<Engine.VPT.Bumper.Bumper, BumperData>
	{
		protected override string[] Children => new []{"Base", "Cap", "Ring", "Skirt"};

		protected override Engine.VPT.Bumper.Bumper GetItem()
		{
			return new Engine.VPT.Bumper.Bumper(data);
		}
	}
}
