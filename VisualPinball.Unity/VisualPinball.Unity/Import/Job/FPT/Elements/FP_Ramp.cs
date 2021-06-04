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
    public class FP_Ramp
    {
        public Color color;
        public string texture;
        public int ramp_profile;
        public int start_height;
        public int start_width;
        public int end_height;
        public int end_width;
        public int left_side_height;
        public int right_side_height;
        public int reflects_off_playfield;
        public bool sphere_mapping;
        public int transparency;
        public int locked;
        public int layer;
        public List<FPShapePoint> ramp_points;
    }
}
