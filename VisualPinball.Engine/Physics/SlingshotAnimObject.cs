using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.Physics
{
	public class SlingshotAnimObject : AnimObject
	{
		public float TimeReset;
		public bool Animations = false;
		public bool IFrame;

		public void Animate(PlayerPhysics physics) {
			if (!IFrame && TimeReset != 0 && Animations) {
				IFrame = true;

			} else if (IFrame && TimeReset < physics.TimeMsec) {
				IFrame = false;
				TimeReset = 0;
			}
		}
	}
}
