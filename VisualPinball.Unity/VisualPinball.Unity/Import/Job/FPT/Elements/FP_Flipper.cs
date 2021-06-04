// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.FP
{
    public class FP_Flipper
    {
		public string name;
		public Vertex2D position;
        public string model;
        public string surface;
        public string texture;
        public Color color;
        public int start_angle;
        public int strength;
        public int swing;
        public int elasticity;
        public string flipper_up_sound;
        public string flipper_down_sound;

		public static float convertElasticity(int elast)
		{
			float elasticity = 0.5f;//0.6f;
			switch (elast)
			{
				case 0: elasticity = 0.2f; break;// 0.4f;break;//0.4
				case 1: elasticity = 0.5f; break;// 0.5f;break;//0.6
				case 2: elasticity = 0.8f; break;// 0.6f;break;//0.8
			}
			return elasticity;
		}

		public Engine.VPT.Flipper.FlipperData ToVpx()
		{
			var vpx = new Engine.VPT.Flipper.FlipperData(name, 0F, 0F);

			// Todo: coherent with fp basic flipper
			vpx.BaseRadius = 21.5F;
			vpx.EndRadius = 13.0F;
			vpx.Height = 50.0F;
			vpx.RubberThickness = 7.0f;
			vpx.RubberHeight = 19.0f;
			vpx.RubberWidth = 24.0f;
			vpx.FlipperRadiusMax = 130.0f; // Length at easy level
			vpx.FlipperRadiusMin = 130.0f; // Length at herd level

			vpx.StartAngle = start_angle; // TODO negative?
			vpx.EndAngle = start_angle + swing;

			vpx.Center = FptUtils.mm2VpUnits(position);

			vpx.Image = texture;
			vpx.Surface = surface;

			vpx.Strength = strength * 500F;
			vpx.Elasticity = convertElasticity(elasticity);

			//TODO:
			//vpx.Material;
			//vpx.RubberMaterial
			
			return vpx;
		}
	}

	
}
