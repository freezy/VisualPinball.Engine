// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Ball
{
	public class BallData : ItemData
	{
		public readonly uint Id;
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

		public BallData(uint id, float radius, float mass, float bulbIntensityScale) : base(string.Empty)
		{
			Id = id;
			Radius = radius;
			Mass = mass;
			BulbIntensityScale = bulbIntensityScale;
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			// balls aren't persisted
		}

		public override string GetName() => $"Ball{Id}";
		public override void SetName(string name) {}

	}
}
