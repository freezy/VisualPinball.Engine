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
    public class FP_AutoPlunger
    {
        public Vertex2D position;
        public string texture;
        public Color color;
        public int rotation;

        public int strength;

        public bool reflects_off_playfield;

        public string playfield;

        public bool include_v_cut;
        public Vertex2D  v_cut_position;
        public int v_cut_lenght;
        public string v_cut_texture;
        public Color v_cut_color;

        public string solenoid;

        public int locked;
        public int layer;
    }
}
