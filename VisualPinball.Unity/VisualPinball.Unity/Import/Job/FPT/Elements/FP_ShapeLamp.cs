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

using System.Collections.Generic;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.FP
{
    public class FP_ShapeLamp
    {
        public Vertex2D texture_position;
        public Vertex2D halo_position;
        public string lens_texture;
        public string surface;
        public Color lit_color;
        //public int auto_set_unlit_color;
        public Color unlit_color;
        public int border_width;
        public Color border_color;
        public bool cookie_cut;
        public int glow_radius;
        public int state;
        public int blink_interval;
        public string blink_pattern;
        public bool object_appers_on;
        public List<FPShapePoint> shape_points;
    }
}
