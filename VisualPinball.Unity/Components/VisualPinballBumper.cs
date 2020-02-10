#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity.Components
{
	public class VisualPinballBumper : ItemComponent<Bumper, BumperData>
	{
		protected override string[] Children => new []{"Base", "Cap", "Ring", "Skirt"};

		protected override Bumper GetItem()
		{
			return new Bumper(data);
		}

		protected override void OnDataSet()
		{
		}

		protected override void OnFieldsUpdated()
		{
		}
	}
}
