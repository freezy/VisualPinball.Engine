using System;
using System.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Ball
{
	public class BallData : ItemData
	{
		public float Radius;
		public float Mass;
		public float BulbIntensityScale;
		public Color Color = new Color(0xfffffff, ColorFormat.Bgr);

		public string EnvironmentMap = string.Empty;
		public string FrontDecal = string.Empty;
		public bool DecalMode = false;
		public bool IsReflectionEnabled = true;
		public float PlayfieldReflectionStrength = 1.0f;
		public bool ForceReflection = false;

		public BallData() : this(25f, 1f, 1f)
		{
		}

		public BallData(float radius, float mass, float bulbIntensityScale) : base(string.Empty)
		{
			Radius = radius;
			Mass = mass;
			BulbIntensityScale = bulbIntensityScale;
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			// balls aren't persisted
		}

		public override string GetName()
		{
			throw new NotImplementedException();
		}
	}
}
