namespace VisualPinball.Unity
{

	/// <summary>
	/// Event data when the game item either reaches resting or end
	/// position.
	/// </summary>
	public struct RotationEventArgs
	{
		/// <summary>
		/// Angle speed with which the new position was reached.
		/// </summary>
		public float AngleSpeed;
	}
}
