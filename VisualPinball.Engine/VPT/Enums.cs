namespace VisualPinball.Engine.VPT
{
	public static class BackglassIndex
	{
		public const int Desktop = 0;
		public const int Fullscreen = 1;
		public const int FullSingleScreen = 2;
	}

	public static class ItemType
	{
		public const int Surface = 0;
		public const int Flipper = 1;
		public const int Timer = 2;
		public const int Plunger = 3;
		public const int Textbox = 4;
		public const int Bumper = 5;
		public const int Trigger = 6;
		public const int Light = 7;
		public const int Kicker = 8;
		public const int Decal = 9;
		public const int Gate = 10;
		public const int Spinner = 11;
		public const int Ramp = 12;
		public const int Table = 13;
		public const int LightCenter = 14;
		public const int DragPoint = 15;
		public const int Collection = 16;
		public const int DispReel = 17;
		public const int LightSeq = 18;
		public const int Primitive = 19;
		public const int Flasher = 20;
		public const int Rubber = 21;
		public const int HitTarget = 22;
		public const int Count = 23;
		public const uint Invalid = 0xffffffff;
	}

	public static class LightStatus
	{
		public const int LightStateOff = 0;
		public const int LightStateOn = 1;
		public const int LightStateBlinking = 2;
	}
}
