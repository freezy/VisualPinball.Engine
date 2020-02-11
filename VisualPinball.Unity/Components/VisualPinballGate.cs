#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballGate : ItemComponent<Gate, GateData>
	{
		protected override string[] Children => new []{"Wire", "Bracket"};

		protected override Gate GetItem()
		{
			return new Gate(data);
		}
	}
}
