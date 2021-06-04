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
    public class FP_Wall
    {
        public string surface;
        public Color top_color;
        public string top_texture;
        public bool cookie_cut;
        public bool sphere_map_the_top;
        public Color side_color;
        public string side_texture;
        public int transparency;
        public float height;
        public float width;
        public int material_type;
        public bool sphere_map_the_side;
        public bool flat_shading;
        public bool surface_is_a_playfield;
        public bool reflects_off_playfield;
        //public string enamel_map;
        //public int reflect_texture;
        //public string playfield;
        public bool dropped;
        public bool collidable;
        public bool render_object;
        public bool generate_hit_event;
        public List<FPShapePoint> shape_points;
    }
}
