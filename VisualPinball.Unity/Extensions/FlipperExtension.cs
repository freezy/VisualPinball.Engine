using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Components;

namespace VisualPinball.Unity.Extensions
{
	public static class FlipperExtension
	{
		public static VisualPinballFlipper AddComponent(this Flipper flipper, GameObject go)
		{
			var component = go.AddComponent<VisualPinballFlipper>();
			component.SetData(flipper.Data);
			return component;
		}

		public static VisualPinballTable AddComponent(this Table table, GameObject go)
		{
			var component = go.AddComponent<VisualPinballTable>();
			component.SetData(table.Data);
			return component;
		}
	}
}
