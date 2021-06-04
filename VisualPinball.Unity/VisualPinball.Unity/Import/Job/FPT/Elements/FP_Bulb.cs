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
    public class FP_Bulb
    {
        public Vertex2D position;
        public string model;
        public string surface;
        public bool render_model;
        public string lens_texture;
        public int glow_radius;
        public bool ordered_halo_glow;
        public Color lit_color;
        //public bool auto_set_unlit_color;
        public Color unlit_color;
        public bool crystal;
        public int rotation;
        public bool reflects_off_playfield;
        public int state;
        public int blink_interval;
        public string blink_pattern;
        public bool object_appers_on;

        public bool is_flasher = false;

        public void SetupFromFlasher(FP_Flasher f)
        {
            position = f.position;
            model = f.model;
            surface = f.surface;
            render_model = true;

            lens_texture = "";
            glow_radius = 200;
            ordered_halo_glow = f.ordered_halo_glow;
            lit_color = f.lit_color;
            unlit_color = f.unlit_color;
            //auto_set_unlit_color = f.auto_set_unlit_color;
            crystal = true;
            rotation = f.rotation;
            reflects_off_playfield = f.reflects_off_playfield;
            state = f.state;
            blink_interval = f.blink_interval;
            blink_pattern = f.blink_pattern;
            object_appers_on = true;

            //locked = f.locked;
            //layer = f.layer;

            is_flasher = true;
        }
    }
}
