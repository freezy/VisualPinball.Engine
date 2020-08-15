// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global

namespace VisualPinball.Unity
{
	public class DebugFlipperSlider
	{
		public readonly string Label;
		public readonly DebugFlipperSliderParam Param;
		public readonly float MinValue;
		public readonly float MaxValue;

		public DebugFlipperSlider(string label, DebugFlipperSliderParam param, float minValue, float maxValue)
		{
			Label = label;
			Param = param;
			MinValue = minValue;
			MaxValue = maxValue;
		}
	}

	public enum DebugFlipperSliderParam
	{
		Acc = 1,
		OffScale = 2,
		OnNearEndScale = 3,
		NumOfDegreeNearEnd = 4,
		Mass = 5,
	}
}
