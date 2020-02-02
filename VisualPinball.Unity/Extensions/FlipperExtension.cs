using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Extensions
{
	public static class FlipperExtension
	{
		public static FlipperComponent AddComponent(this Flipper flipper, GameObject go)
		{
			var component = go.AddComponent<FlipperComponent>();
			component.FlipperData = flipper.Data;
			return component;
		}

		public static TableComponent AddComponent(this Table table, GameObject go)
		{
			var component = go.AddComponent<TableComponent>();
			component.Data = table.Data;
			return component;
		}
	}
}
