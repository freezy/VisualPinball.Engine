using System;
using System.Globalization;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Plunger
{
	/// <summary>
	/// Plunger 3D shape descriptor
	/// </summary>
	public struct PlungerDesc
	{
		/// <summary>
		/// Number of coordinates in the lathe list. If there are no
		/// lathe points, this is the flat plunger, which we draw as
		/// an alpha image on a simple flat rectangular surface.
		/// </summary>
		public int n;

		/// <summary>
		/// List of lathe coordinates.
		/// </summary>
		public PlungerCoord[] c;

		public PlungerDesc(PlungerCoord[] c) : this()
		{
			this.c = c;
			n = c.Length;
		}

		public static PlungerDesc GetModern()
		{
			return new PlungerDesc(PlungerCoord.modernCoords);
		}

		public static PlungerDesc GetFlat()
		{
			return new PlungerDesc(new PlungerCoord[0]);
		}

		public static PlungerDesc GetCustom(PlungerData data, float beginY, float springMinSpacing,
			out float rody, out float springGauge, out float springRadius,
			out float springLoops, out float springEndLoops)
		{
			// Several of the entries are fixed:
			// shaft x 2 (top, bottom)
			// ring x 6 (inner top, outer top x 2, outer bottom x 2, inner bottom)
			// ring gap x 2 (top, bottom)
			// tip bottom inner x 1
			// first entry in custom tip list (there's always at least one;
			//    even if it's blank, we read the empty entry as "0,0" )
			var numCoords = 2 + 6 + 2 + 1 + 1;

			// Split the tip list.
			// Entries are separated by semicolons.
			var tipShapes = string.IsNullOrEmpty(data.TipShape)
				? new string[0]
				: data.TipShape.Split(';');
			var numTips = tipShapes.Length;
			numCoords += numTips - 1;

			// allocate the descriptor and the coordinate array
			var desc = new PlungerDesc(new PlungerCoord[numCoords]);

			// figure the tip lathe descriptor from the shape point list
			//var c = customDesc.c;
			float tipLen = 0;
			int i;
			for (i = 0; i < tipShapes.Length; i++) {

				// Parse the entry: "yOffset, diam".  yOffset is the
				// offset (in table distance units) from the previous
				// point.  "diam" is the diameter (relative to the
				// nominal width of the plunger, as given by the width
				// property) of the tip at this point.  1.0 means that
				// the diameter is the same as the nominal width; 0.5
				// is half the width.
				var tipShape = tipShapes[i];
				var ts = tipShape.Trim().Split(' ');
				ref var c = ref desc.c[i];

				try {
					c.y = int.Parse(ts[0], CultureInfo.InvariantCulture);
				} catch (FormatException) {
					c.y = 0;
				}

				try {
					var v = ts.Length > 1 ? ts[1] : "0.0";
					c.r = float.Parse(v.StartsWith(".") ? "0" + v : v, CultureInfo.InvariantCulture) * 0.5f;
				} catch (FormatException) {
					c.r = 0;
				}

				// each entry has to have a higher y value than the last
				if (c.y < tipLen) {
					c.y = tipLen;
				}

				// update the tip length so far
				tipLen = c.y;
			}

			// Figure the normals and the texture coordinates
			var prevCoord = new PlungerCoord(0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
			for (i = 0; i < numTips; i++) {
				ref var coord = ref desc.c[i];

				// Figure the texture coordinate.  The tip is always
				// the top 25% of the overall texture; interpolate the
				// current lathe point's position within that 25%.
				coord.tv = 0.24f * coord.y / tipLen;

				// Figure the normal as the average of the surrounding
				// surface normals.
				ref var nextCoord = ref desc.c[i + 1 < numTips ? i + 1 : i];
				var x0 = prevCoord.r;
				var y0 = prevCoord.y;
				var x1 = nextCoord.r;
				var y1 = nextCoord.y;
				var yd = y1 - y0;
				var xd = (x1 - x0) * data.Width;
				//const float th = atan2f(yd, xd);
				//c->nx = sinf(th);
				//c->ny = -cosf(th);
				var r = MathF.Sqrt(xd * xd + yd * yd);
				coord.nx = yd / r;
				coord.ny = -xd / r;

				prevCoord = coord;
			}

			// add the inner edge of the tip (abutting the rod)
			var rRod = data.RodDiam / 2.0f;
			var y = tipLen;
			desc.c[i++].Set(rRod, y, 0.24f, 1.0f, 0.0f);

			// add the gap between tip and ring (texture is in the rod
			// quadrant of overall texture, 50%-75%)
			desc.c[i++].Set(rRod, y, 0.51f, 1.0f, 0.0f);
			y += data.RingGap;
			desc.c[i++].Set(rRod, y, 0.55f, 1.0f, 0.0f);

			// add the ring (texture is in the ring quadrant, 25%-50%)
			var rRing = data.RingDiam / 2.0f;
			desc.c[i++].Set(rRod, y, 0.26f, 0.0f, -1.0f);
			desc.c[i++].Set(rRing, y, 0.33f, 0.0f, -1.0f);
			desc.c[i++].Set(rRing, y, 0.33f, 1.0f, 0.0f);
			y += data.RingWidth;
			desc.c[i++].Set(rRing, y, 0.42f, 1.0f, 0.0f);
			desc.c[i++].Set(rRing, y, 0.42f, 0.0f, 1.0f);
			desc.c[i++].Set(rRod, y, 0.49f, 0.0f, 1.0f);

			// set the spring values from the properties
			springRadius = data.SpringDiam * 0.5f;
			springGauge = data.SpringGauge;
			springLoops = data.SpringLoops;
			springEndLoops = data.SpringEndLoops;

			// add the top of the shaft (texture is in the 50%-75% quadrant)
			desc.c[i++].Set(rRod, y, 0.51f, 1.0f, 0.0f);

			// Figure the fully compressed spring length.  This is
			// the lower bound for the rod length.
			var springMin = (springLoops + springEndLoops) * springMinSpacing;

			// Figure the rod bottom position (rody).  This is the fully
			// retracted tip position (beginy), plus the length of the parts
			// at the end that don't compress with the spring (y), plus the
			// fully retracted spring length.
			rody = beginY + y + springMin;
			desc.c[i++].Set(rRod, rody, 0.74f, 1.0f, 0.0f);

			return desc;
		}
	}
}
