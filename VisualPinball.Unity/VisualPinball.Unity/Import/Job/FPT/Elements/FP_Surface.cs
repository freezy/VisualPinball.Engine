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
    public class FP_Surface
    {
		public string name;
        public Color top_color;
        public string top_texture;
        public bool cookie_cut;
        public bool sphere_map_the_top;
        public Color side_color;
        public string side_texture;
        public int transparency;
        public float top_height;
        public float bottom_height;
        public int material_type;
        public bool sphere_map_the_side;
        public bool flat_shading;
        public bool surface_is_a_playfield;
        public bool reflects_off_playfield;
        public string enamel_map;
        public bool reflect_texture;
        public string playfield;
        public bool dropped;
        public bool collidable;
        public bool render_object;
        public bool generate_hit_event;
        public List<FPShapePoint> shape_points;
		//public BezierSpline CapSpline;
		//public BezierSpline PathSpline;

		public Engine.VPT.Surface.SurfaceData ToVpx()
		{
			var vpx = new Engine.VPT.Surface.SurfaceData(name, FptUtils.FPShapePoint2DragPointData(shape_points));
			vpx.Name = name;

			vpx.HitEvent = generate_hit_event;
			vpx.IsDroppable = true;

			vpx.IsBottomSolid = bottom_height > 0; // ?

			vpx.IsCollidable = collidable;

			vpx.Image = top_texture;
			vpx.SideImage = side_texture;

			// TODO
			// vpx.SideMaterial
			// vpx.TopMaterial
			// vpx.PhysicsMaterial
			// vpx.SlingShotMaterial

			vpx.HeightBottom = FptUtils.mm2VpUnits(bottom_height);
			vpx.HeightTop = FptUtils.mm2VpUnits(top_height);

			vpx.IsTopBottomVisible = render_object;
			vpx.IsSideVisible = render_object;

			vpx.Points = shape_points != null && shape_points.Count > 0;
			//vpx.DragPoints = FptUtils.FPShapePoint2DragPointData(shape_points);

			return vpx;
		}
	}


}
