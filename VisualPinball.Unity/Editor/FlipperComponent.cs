using NLog;
using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	[ExecuteInEditMode]
	public class FlipperComponent : ItemComponent<Flipper, FlipperData>
	{
		public float baseRadius;
		public float endRadius;

		protected override void OnDataSet()
		{
			baseRadius = data.BaseRadius;
			endRadius = data.EndRadius;
		}

		protected override Flipper GetItem(FlipperData d)
		{
			return new Flipper(d);
		}

		protected override string[] GetChildren()
		{
			return new[] {"Base", "Rubber"};
		}

		private void OnValidate()
		{
			if (baseRadius != data.BaseRadius || endRadius != data.EndRadius) {
				data.BaseRadius = baseRadius;
				data.EndRadius = endRadius;
				UpdateMeshes();
			}
		}
	}
}
