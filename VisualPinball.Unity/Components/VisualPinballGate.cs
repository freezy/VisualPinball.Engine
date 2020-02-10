#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity.Components
{
	public class VisualPinballGate : ItemComponent<Gate, GateData>
	{
		protected override string[] Children => new []{"Wire", "Bracket"};

		protected override Gate GetItem()
		{
			return new Gate(data);
		}

		protected override void OnDataSet()
		{
		}

		protected override void OnFieldsUpdated()
		{
		}
	}
}
