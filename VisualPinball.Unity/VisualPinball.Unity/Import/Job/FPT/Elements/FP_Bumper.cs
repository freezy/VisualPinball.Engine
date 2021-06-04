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
    public class FP_Bumper
    {
        public Vertex2D position;
        public int rotation;
        public string cap_model;
        public string base_model;
        public string surface;
        public string cap_texture;
        public bool passive;
        public bool trigger_skirt;

        public Color skirt_color;
        public Color lit_color;

        public bool auto_set_unlit_color;
        public bool ordered_halo_glow;

        public Color unlit_color;
        public Color base_color;

        public bool crystal;

        public bool reflects_off_playfield;
        public int strength;

        public string solenoid_sound;

        public bool flash_when_hit;
        public int state;
        public int blink_interval;
        public string blink_pattern;

        public int locked;
        public int layer;
    }
}
