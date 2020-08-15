namespace VisualPinball.Unity
{
	public static class ColorExtensions
	{
		public static UnityEngine.Color ToUnityColor(this Engine.Math.Color color)
		{
			return new UnityEngine.Color(color.R, color.G, color.B, color.A);
		}

		public static Engine.Math.Color ToEngineColor(this UnityEngine.Color color)
		{
			UnityEngine.Color32 c32 = color;
			return new Engine.Math.Color( c32.r, c32.g, c32.b, c32.a );
		}
	}
}
