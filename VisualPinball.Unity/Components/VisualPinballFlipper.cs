#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballFlipper : ItemComponent<Flipper, FlipperData>
	{
		[Header("Flipper")]
		public float BaseRadius;
		public float EndRadius;
		public float Length;
		public float Height;

		[Header("Rubber")]
		public float RubberHeight;
		public float RubberThickness;
		public float RubberWidth;

		protected override string[] Children => new []{"Base", "Rubber"};

		protected override void OnFieldsUpdated()
		{
			data.BaseRadius = BaseRadius;
			data.EndRadius = EndRadius;
			data.FlipperRadius = Length;
			data.Height = Height;
			data.RubberHeight = RubberHeight;
			data.RubberThickness = RubberThickness;
			data.RubberWidth = RubberWidth;
		}

		protected override void OnDataSet()
		{
			BaseRadius = data.BaseRadius;
			EndRadius = data.EndRadius;
			Length = data.FlipperRadius;
			Height = data.Height;
			RubberHeight = data.RubberHeight;
			RubberThickness = data.RubberThickness;
			RubberWidth = data.RubberWidth;
		}

		protected override Flipper GetItem()
		{
			return new Flipper(data);
		}

		protected override bool ShouldRebuildMesh()
		{
			return BaseRadius != data.BaseRadius ||
			       EndRadius != data.EndRadius ||
			       Length != data.FlipperRadius ||
			       Height != data.Height ||
			       RubberHeight != data.RubberHeight ||
			       RubberThickness != data.RubberThickness ||
			       RubberWidth != data.RubberWidth;
		}
	}
}
