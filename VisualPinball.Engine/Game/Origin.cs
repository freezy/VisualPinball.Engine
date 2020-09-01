namespace VisualPinball.Engine.Game
{
	public enum Origin
	{
		/// <summary>
		/// Keeps the origin the same as in Visual Pinball. <p/>
		///
		/// This means that the object must additional retrieve a
		/// transformation matrix.
		/// </summary>
		Original,

		/// <summary>
		/// Transforms all vertices so their origin is the global origin. <p/>
		///
		/// No additional transformation matrices must be applied if the object
		/// is static.
		/// </summary>
		Global
	}
}
