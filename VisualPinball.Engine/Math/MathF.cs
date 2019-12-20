
namespace VisualPinball.Engine.Math
{
	/// <summary>
	/// A System.Math wrapper for floats.
	/// </summary>
	///
	/// <see href="https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Mathf.cs">Original Source</see>
	public class MathF
	{

		// The infamous ''3.14159265358979...'' value (RO).
		public const float PI = (float)System.Math.PI;

		// Returns the sine of angle /f/ in radians.
		public static float Sin(float f) { return (float)System.Math.Sin(f); }

		// Returns the cosine of angle /f/ in radians.
		public static float Cos(float f) { return (float)System.Math.Cos(f); }

		// Returns the absolute value of /f/.
		public static float Abs(float f) { return (float)System.Math.Abs(f); }

		// Returns square root of /f/.
		public static float Sqrt(float f) { return (float)System.Math.Sqrt(f); }
	}
}
