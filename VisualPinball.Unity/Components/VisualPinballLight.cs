#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Components
{
	public class VisualPinballLight : ItemComponent<Light, LightData>
	{
		protected override string[] Children => null;

		protected override Light GetItem()
		{
			return new Light(data);
		}

		protected override void OnDataSet()
		{
		}

		protected override void OnFieldsUpdated()
		{
		}
	}
}
