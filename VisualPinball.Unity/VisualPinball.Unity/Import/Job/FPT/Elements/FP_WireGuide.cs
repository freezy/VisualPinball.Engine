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
    public class FP_WireGuide
    {
        public string surface;
        public Color color;
        public string texture;
        public bool sphere_map;
        public int height;
        public int width;
        public int mark_as_ornamental;
        public bool reflects_off_playfield;
        public List<FPShapePoint> shape_points;
    }
}
