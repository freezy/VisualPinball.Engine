namespace VisualPinball.Unity.Extensions
{
	public static class Color
	{
		public static UnityEngine.Color ToUnityColor(this Engine.Math.Color color)
		{
			return new UnityEngine.Color(color.R, color.G, color.B, color.A);
		}
	}
}
