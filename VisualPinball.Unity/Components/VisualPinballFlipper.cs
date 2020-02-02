// ReSharper disable CompareOfFloatsByEqualityOperator

using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballFlipper : ItemComponent<Flipper, FlipperData>
	{
		[Header("Flipper")]
		public float baseRadius;
		public float endRadius;
		public float length;
		public float height;

		[Header("Rubber")]
		public float rubberHeight;
		public float rubberThickness;
		public float rubberWidth;

		protected override void OnFieldsUpdated()
		{
			data.BaseRadius = baseRadius;
			data.EndRadius = endRadius;
			data.FlipperRadius = length;
			data.Height = height;
			data.RubberHeight = rubberHeight;
			data.RubberThickness = rubberThickness;
			data.RubberWidth = rubberWidth;
		}

		protected override void OnDataSet()
		{
			baseRadius = data.BaseRadius;
			endRadius = data.EndRadius;
			length = data.FlipperRadius;
			height = data.Height;
			rubberHeight = data.RubberHeight;
			rubberThickness = data.RubberThickness;
			rubberWidth = data.RubberWidth;
		}

		protected override Flipper GetItem(FlipperData d)
		{
			return new Flipper(d);
		}

		protected override string[] GetChildren()
		{
			return new[] {"Base", "Rubber"};
		}

		protected override bool ShouldRebuildMesh()
		{
			return baseRadius != data.BaseRadius ||
			       endRadius != data.EndRadius ||
			       length != data.FlipperRadius ||
			       height != data.Height ||
			       rubberHeight != data.RubberHeight ||
			       rubberThickness != data.RubberThickness ||
			       rubberWidth != data.RubberWidth;
		}
	}
}
