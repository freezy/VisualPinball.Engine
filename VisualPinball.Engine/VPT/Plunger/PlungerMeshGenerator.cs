using NLog;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class PlungerMeshGenerator
	{
		public const string FlatName = "Flat";
		public const string RodName = "Rod";
		public const string SpringName = "Spring";

		public int NumFrames { get; }

		private readonly PlungerData _data;
		private PlungerDesc _desc;

		private readonly float _beginY;
		private readonly float _endY;
		private readonly float _invScale;
		private readonly float _dyPerFrame;
		private readonly int _circlePoints;
		private readonly float _springMinSpacing;
		private readonly float _cellWid;
		private readonly int _srcCells;

		private float _zScale;
		private float _zHeight;
		private float _springLoops;
		private float _springEndLoops;
		private float _springGauge;
		private float _springRadius;
		private float _rodY;
		private int _lathePoints;
		private int _latheVts;
		private int _springVts;
		private int _latheIndices;
		private int _springIndices;

		private const int PlungerFrameCount = 25;
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PlungerMeshGenerator(PlungerData data)
		{
			_data = data;

			var stroke = data.Stroke;
			_beginY = data.Center.Y;
			_endY = data.Center.Y - stroke;
			NumFrames = (int) (stroke * (float) (PlungerFrameCount / 80.0)) + 1; // 25 frames per 80 units travel
			_invScale = NumFrames > 1 ? 1.0f / (NumFrames - 1) : 0.0f;
			_dyPerFrame = (_endY - _beginY) * _invScale;
			_circlePoints = data.Type == PlungerType.PlungerTypeFlat ? 0 : 24;
			_springLoops = 0.0f;
			_springEndLoops = 0.0f;
			_springGauge = 0.0f;
			_springRadius = 0.0f;
			_springMinSpacing = 2.2f;
			_rodY = _beginY + data.Height;

			// note the number of cells in the source image
			_srcCells = data.AnimFrames;
			if (_srcCells < 1) {
				_srcCells = 1;
			}

			// figure the width in relative units (0..1) of each cell
			_cellWid = 1.0f / _srcCells;

			_desc = GetPlungerDesc();
		}

		public void Init(Table.Table table)
		{
			_zHeight = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) + _data.ZAdjust;
			_zScale = table.GetScaleZ();
			_desc = GetPlungerDesc();
		}

		public RenderObjectGroup GetRenderObjects(int frame, Table.Table table, Origin origin, bool asRightHanded = true)
		{
			Init(table);

			// todo
			var translationMatrix = Matrix3D.Identity;
			var material = new PbrMaterial(table.GetMaterial(_data.Material), table.GetTexture(_data.Image));

			// flat plunger
			if (_data.Type == PlungerType.PlungerTypeFlat) {
				var flatMesh = BuildFlatMesh(frame);
				return new RenderObjectGroup(_data.Name, "Plungers", translationMatrix,
					new RenderObject(
						FlatName,
						asRightHanded ? flatMesh.Transform(Matrix3D.RightHanded) : flatMesh,
						material,
						true
					)
				) { ForceChild = true };
			}

			CalculateArraySizes();
			var rodMesh = BuildRodMesh(frame);

			// custom plunger
			if (_data.Type == PlungerType.PlungerTypeCustom) {
				var springMesh = BuildSpringMesh(frame);

				return new RenderObjectGroup(_data.Name, "Plungers", translationMatrix,
					new RenderObject(
						RodName,
						asRightHanded ? rodMesh.Transform(Matrix3D.RightHanded) : rodMesh,
						material,
						true
					),
					new RenderObject(
						SpringName,
						asRightHanded ? springMesh.Transform(Matrix3D.RightHanded) : springMesh,
						material,
						true
					)
				);
			}

			// modern plunger
			return new RenderObjectGroup(_data.Name, "Plungers", translationMatrix,
				new RenderObject(
					RodName,
					asRightHanded ? rodMesh.Transform(Matrix3D.RightHanded) : rodMesh,
					material,
					true
				)
			) { ForceChild = true };
		}

		private PlungerDesc GetPlungerDesc()
		{
			switch (_data.Type) {
				case PlungerType.PlungerTypeModern:
					return PlungerDesc.GetModern();

				case PlungerType.PlungerTypeFlat:
					return PlungerDesc.GetFlat();

				case PlungerType.PlungerTypeCustom:
					return PlungerDesc.GetCustom(_data, _beginY, _springMinSpacing,
						out _rodY, out _springGauge, out _springRadius,
						out _springLoops, out _springEndLoops);
			}
			Logger.Warn("Unknown plunger type {0}.", _data.Type);
			return PlungerDesc.GetModern();
		}

		private void CalculateArraySizes()
		{
			// get the number of lathe points from the descriptor
			_lathePoints = _desc.n;

			// For all other plungers, we render one circle per lathe
			// point.  Each circle has 'circlePoints' vertices.  We
			// also need to render the spring:  this consists of 3
			// spirals, where each spiral has 'springLoops' loops
			// times 'circlePoints' vertices.
			_latheVts = _lathePoints * _circlePoints;
			_springVts = (int)((_springLoops + _springEndLoops) * _circlePoints) * 3;

			// For the lathed section, we need two triangles == 6
			// indices for every point on every lathe circle past
			// the first.  (We connect pairs of lathe circles, so
			// the first one doesn't count: two circles -> one set
			// of triangles, three circles -> two sets, etc).
			_latheIndices = 6 * _circlePoints * (_lathePoints - 1);

			// For the spring, we need 4 triangles == 12 indices
			// for every matching set of three vertices on the
			// three spirals, not counting the first set (as above,
			// we're connecting adjacent sets, so the first doesn't
			// count).  We already counted the total number of
			// vertices, so divide that by 3 to get the number
			// of sets.  12*vts/3 = 4*vts.
			//
			// The spring only applies to the custom plunger.
			_springIndices = 0;
			if (_data.Type == PlungerType.PlungerTypeCustom) {
				_springIndices = 4 * _springVts - 12;
				if (_springVts < 0) {
					_springIndices = 0;
				}
			}
		}

		/// <summary>
		/// Flat plunger - overlay the alpha image on a rectangular surface.
		/// </summary>
		/// <param name="frame">Animation frame</param>
		/// <returns></returns>
		private Mesh BuildFlatMesh(int frame)
		{
			return new Mesh("flat") {
				Vertices = BuildFlatVertices(frame),
				Indices = new[] {0, 1, 2, 2, 3, 0}
			};
		}

		public Vertex3DNoTex2[] BuildFlatVertices(int frame)
		{
			// Figure the corner coordinates.
			//
			// The tip of the plunger for this frame is at 'height', which is the
			// nominal y position (m_d.m_v.y) plus the portion of the stroke length
			// for the current frame.  (The 0th frame is the most retracted position;
			// the cframe-1'th frame is the most forward position.)  The base is at
			// the nominal y position plus m_d.m_height.
			var xLt = _data.Center.X - _data.Width;
			var xRt = _data.Center.X + _data.Width;
			var yTop = _beginY + _dyPerFrame * frame;
			var yBot = _beginY + _data.Height;

			// Figure the z coordinate.
			//
			// For the shaped plungers, the vertical extent is determined by placing
			// the long axis at the plunger's nominal width (m_d.m_width) above the
			// playfield (or whatever the base surface is).  Since those are modeled
			// roughly as cylinders with the main shaft radius at about 1/4 the nominal
			// width, the top point is at about 1.25 the nominal width and the bulk
			// is between 1x and 1.25x the nominal width above the base surface.  To
			// get approximately the same effect, we place our rectangular surface at
			// 1.25x the width above the base surface.  The table author can tweak this
			// using the ZAdjust property, which is added to the zHeight base level.
			var z = (_zHeight + _data.Width * 1.25f) * _zScale;

			// Figure out which animation cell we're using.  The source image might not
			// (and probably does not) have the same number of cells as the frame list
			// we're generating, since our frame count depends on the stroke length.
			// So we need to interpolate between the image cells and the generated frames.
			//
			// The source image is arranged with the fully extended image in the leftmost
			// cell and the fully retracted image in the rightmost cell.  Our frame
			// numbering is just the reverse, so figure the cell number in right-to-left
			// order to simplify the texture mapping calculations.
			var cellIdx = _srcCells - 1 - (int) (frame * (float) _srcCells / NumFrames + 0.5f);
			if (cellIdx < 0) {
				cellIdx = 0;
			}

			// Figure the texture coordinates.
			//
			// The y extent (tv) maps to the top portion of the image with height
			// proportional to the current frame's height relative to the overall height.
			// Our frames vary in height to display the motion of the plunger.  The
			// animation cells are all the same height, so we need to map to the
			// proportional vertical share of the cell.  The images in the cells are
			// top-justified, so we always start at the top of the cell.
			//
			// The x extent is the full width of the current cell.
			var tuLocal = _cellWid * cellIdx;
			var tvLocal = (yBot - yTop) / (_beginY + _data.Height - _endY);


			// Fill in the four corner vertices.
			// Vertices are (in order): bottom left, top left, top right, bottom right.
			return new[] {
				new Vertex3DNoTex2(xLt, yBot, z, 0.0f, 0.0f, -1.0f, tuLocal, tvLocal),
				new Vertex3DNoTex2(xLt, yTop, z, 0.0f, 0.0f, -1.0f, tuLocal, 0.0f),
				new Vertex3DNoTex2(xRt, yTop, z, 0.0f, 0.0f, -1.0f, tuLocal + _cellWid, 0.0f),
				new Vertex3DNoTex2(xRt, yBot, z, 0.0f, 0.0f, -1.0f, tuLocal + _cellWid, tvLocal)
			};
		}

		/// <summary>
		/// Build the rod mesh
		///
		/// Go around in a circle starting at the top, stepping through 'circlePoints'
		/// angles along the circle. Start the texture mapping in the middle, so that
		/// the center line of the texture maps to the center line of the top of the
		/// cylinder surface. Work outwards on the texture to wrap it around the
		/// cylinder.
		/// </summary>
		/// <param name="frame"></param>
		/// <returns></returns>
		private Mesh BuildRodMesh(int frame)
		{
			var mesh = new Mesh("rod") {
				Vertices = BuildRodVertices(frame),
				Indices = new int[_latheIndices]
			};

			// set up the vertex list for the lathe circles
			var k = 0;
			for (int l = 0, offset = 0; l < _circlePoints; l++, offset += _lathePoints) {
				for (var m = 0; m < _lathePoints - 1; m++) {
					mesh.Indices[k++] = (m + offset) % _latheVts;
					mesh.Indices[k++] = (m + offset + _lathePoints) % _latheVts;
					mesh.Indices[k++] = (m + offset + 1 + _lathePoints) % _latheVts;

					mesh.Indices[k++] = (m + offset + 1 + _lathePoints) % _latheVts;
					mesh.Indices[k++] = (m + offset + 1) % _latheVts;
					mesh.Indices[k++] = (m + offset) % _latheVts;
				}
			}

			return mesh;
		}

		public Vertex3DNoTex2[] BuildRodVertices(int frame)
		{
			if (_lathePoints == 0) {
				CalculateArraySizes();
			}
			var vertices = new Vertex3DNoTex2[_latheVts];
			var yTip = _beginY + _dyPerFrame * frame;

			var tu = 0.51f;
			var stepU = 1.0f / _circlePoints;
			var i = 0;
			for (var l = 0; l < _circlePoints; l++, tu += stepU) {

				// Go down the long axis, adding a vertex for each point
				// in the descriptor list at the current lathe angle.
				if (tu > 1.0f) {
					tu -= 1.0f;
				}

				var angle = (float) (MathF.PI * 2.0) / _circlePoints * l;
				var sn = MathF.Sin(angle);
				var cs = MathF.Cos(angle);

				for (var m = 0; m < _lathePoints; m++) {
					ref var c = ref _desc.c[m];

					// get the current point's coordinates
					var y = c.y + yTip;
					var r = c.r;
					var tv = c.tv;

					// the last coordinate is always the bottom of the rod
					if (m + 1 == _lathePoints) {

						// set the end point
						y = _rodY;

						// Figure the texture mapping for the rod position.  This is
						// important because we draw the rod with varying length -
						// the part that's pulled back beyond the 'rodY' point is
						// hidden.  We want the texture to maintain the same apparent
						// position and scale in each frame, so we need to figure the
						// proportional point of the texture at our cut-off point on
						// the object surface.
						var ratio = frame * _invScale;
						tv = vertices[m - 1].Tv + (tv - vertices[m - 1].Tv) * ratio;
					}

					vertices[i++] = new Vertex3DNoTex2 {
						X = r * (sn * _data.Width) + _data.Center.X,
						Y = y,
						Z = (r * (cs * _data.Width) + _data.Width + _zHeight) * _zScale,
						Nx = c.nx * sn,
						Ny = c.ny,
						Nz = -c.nx * cs,
						Tu = tu,
						Tv = tv
					};
				}
			}

			return vertices;
		}

		/// <summary>
		/// Build the spring.
		///
		/// We build this as wedge shape wrapped around a spiral. So we actually
		/// have three spirals: the front edge, the top edge, and the back edge.
		/// The y extent is the length of the rod; the rod starts at the
		/// second-to-last entry and ends at the last entry.
		///
		/// But the actual base position of the spring is fixed at the end of the
		/// shaft, which might differ from the on-screen position of the last
		/// point in that the rod can be visually cut off by the length adjustment.
		///
		/// So use the true rod base (rodY) position to figure the spring length.
		/// </summary>
		/// <param name="frame"></param>
		/// <returns></returns>
		private Mesh BuildSpringMesh(int frame)
		{
			var mesh = new Mesh("spring") {
				Vertices = BuildSpringVertices(frame),
				Indices = new int[_springIndices]
			};

			// set up the vertex list for the spring
			var k = 0;
			for (var i = 0; i < mesh.Vertices.Length - 3; i += 3) {

				// Direct3D only renders faces if the vertices are in clockwise
				// order.  We want to render the spring all the way around, so
				// we need to use different vertex ordering for faces that are
				// above and below the vertical midpoint on the spring.  We
				// can use the z normal from the center spiral to determine
				// whether we're at a top or bottom face.  Note that all of
				// the springs in all frames have the same relative position
				// on the spiral, so we can use the first spiral as a proxy
				// for all of them - the only thing about the spring that
				// varies from frame to frame is the length of the spiral.
				var v = mesh.Vertices[i + 1];
				//if (v.Nz <= 0.0f) {
					// top half vertices
					mesh.Indices[k++] = i + 0;
					mesh.Indices[k++] = i + 3;
					mesh.Indices[k++] = i + 1;

					mesh.Indices[k++] = i + 1;
					mesh.Indices[k++] = i + 3;
					mesh.Indices[k++] = i + 4;

					mesh.Indices[k++] = i + 4;
					mesh.Indices[k++] = i + 5;
					mesh.Indices[k++] = i + 2;

					mesh.Indices[k++] = i + 2;
					mesh.Indices[k++] = i + 1;
					mesh.Indices[k++] = i + 4;

				// } else {
				// 	// bottom half vertices
				// 	mesh.Indices[k++] = i + 3;
				// 	mesh.Indices[k++] = i + 0;
				// 	mesh.Indices[k++] = i + 4;
				//
				// 	mesh.Indices[k++] = i + 4;
				// 	mesh.Indices[k++] = i + 0;
				// 	mesh.Indices[k++] = i + 1;
				//
				// 	mesh.Indices[k++] = i + 1;
				// 	mesh.Indices[k++] = i + 2;
				// 	mesh.Indices[k++] = i + 5;
				//
				// 	mesh.Indices[k++] = i + 5;
				// 	mesh.Indices[k++] = i + 1;
				// 	mesh.Indices[k++] = i + 2;
				// }
			}

			return mesh;
		}

		public Vertex3DNoTex2[] BuildSpringVertices(int frame)
		{
			if (_lathePoints == 0) {
				CalculateArraySizes();
			}
			var vertices = new Vertex3DNoTex2[_springVts];

			var springGaugeRel = _springGauge / _data.Width;

			var yTip = _beginY + _dyPerFrame * frame;
			ref var c = ref _desc.c[_lathePoints - 2];
			var y0 = c.y + yTip;
			//var y0 = rodVertices[_latheVts - 2].Y;
			var y1 = _rodY;

			var n = (int) ((_springLoops + _springEndLoops) * _circlePoints);
			var nEnd = (int) (_springEndLoops * _circlePoints);
			var nMain = n - nEnd;
			var yEnd = _springEndLoops * _springGauge * _springMinSpacing;

			var dyMain = (y1 - y0 - yEnd) / (nMain - 1);
			var dyEnd = yEnd / (nEnd - 1);
			var dy = dyEnd;
			var dTheta = (float) (System.Math.PI * 2.0) / (_circlePoints - 1) + MathF.PI / (n - 1);

			var pm = 0;
			for (float theta = MathF.PI, y = y0; n != 0; --n, theta += dTheta, y += dy) {

				if (n == nMain) {
					dy = dyMain;
				}

				if (theta >= (float) (System.Math.PI * 2.0)) {
					theta -= (float) (System.Math.PI * 2.0);
				}

				var sn = MathF.Sin(theta);
				var cs = MathF.Cos(theta);

				// set the point on the front spiral
				vertices[pm++] = new Vertex3DNoTex2 {
					X = _springRadius * (sn * _data.Width) + _data.Center.X,
					Y = y - _springGauge,
					Z = (_springRadius * (cs * _data.Width) + _data.Width + _zHeight) * _zScale,
					Nx = 0.0f,
					Ny = -1.0f,
					Nz = 0.0f,
					Tu = (sn + 1.0f) * 0.5f,
					Tv = 0.76f
				};

				// set the point on the top spiral
				vertices[pm++] = new Vertex3DNoTex2 {
					X = (_springRadius + springGaugeRel / 1.5f) * (sn * _data.Width) + _data.Center.X,
					Y = y,
					Z = ((_springRadius + springGaugeRel / 1.5f) * (cs * _data.Width) + _data.Width + _zHeight) *
					    _zScale,
					Nx = sn,
					Ny = 0.0f,
					Nz = -cs,
					Tu = (sn + 1.0f) * 0.5f,
					Tv = 0.85f
				};

				// set the point on the back spiral
				vertices[pm++] = new Vertex3DNoTex2 {
					X = _springRadius * (sn * _data.Width) + _data.Center.X,
					Y = y + _springGauge,
					Z = (_springRadius * (cs * _data.Width) + _data.Width + _zHeight) * _zScale,
					Nx = 0.0f,
					Ny = 1.0f,
					Nz = 0.0f,
					Tu = (sn + 1.0f) * 0.5f,
					Tv = 0.98f
				};
			}

			return vertices;
		}
	}
}
