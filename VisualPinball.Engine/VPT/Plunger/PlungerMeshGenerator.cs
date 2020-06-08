using NLog;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class PlungerMeshGenerator
	{
		private readonly PlungerData _data;

		private readonly float stroke;
		private readonly float beginy;
		private readonly float endy;
		private readonly float inv_scale;
		private readonly float dyPerFrame;
		private readonly int circlePoints;
		private readonly float springMinSpacing;
		private readonly float cellWid;

		private float zScale;
		private float zheight;
		private int cframes;
		private float springLoops;
		private float springEndLoops;
		private float springGauge;
		private float springRadius;
		private float rody;
		private int srcCells;
		private int indicesPerFrame;
		private int vtsPerFrame;
		private int lathePoints;
		private PlungerDesc desc;

		private const int PLUNGER_FRAME_COUNT = 25;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public PlungerMeshGenerator(PlungerData data)
		{
			_data = data;

			stroke = data.Stroke;
			beginy = data.Center.Y;
			endy = data.Center.Y - stroke;
			cframes = (int) (stroke * (float) (PLUNGER_FRAME_COUNT / 80.0)) + 1; // 25 frames per 80 units travel
			inv_scale = cframes > 1 ? 1.0f / (cframes - 1) : 0.0f;
			dyPerFrame = (endy - beginy) * inv_scale;
			circlePoints = data.Type == PlungerType.PlungerTypeFlat ? 0 : 24;
			springLoops = 0.0f;
			springEndLoops = 0.0f;
			springGauge = 0.0f;
			springRadius = 0.0f;
			springMinSpacing = 2.2f;
			rody = beginy + data.Height;

			// note the number of cells in the source image
			srcCells = data.AnimFrames;
			if (srcCells < 1) {
				srcCells = 1;
			}

			// figure the width in relative units (0..1) of each cell
			cellWid = 1.0f / (float) srcCells;

			desc = GetPlungerDesc();
		}

		public RenderObjectGroup GetRenderObjects(int frame, Table.Table table, bool asRightHanded = true)
		{
			zheight = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) + _data.ZAdjust;
			zScale = table.GetScaleZ();
			desc = GetPlungerDesc();

			// calculate the frame rendering details
			CalculateRenderDetails();

			return null;
		}

		private PlungerDesc GetPlungerDesc()
		{
			switch (_data.Type) {
				case PlungerType.PlungerTypeModern:
					return PlungerDesc.GetModern();
				case PlungerType.PlungerTypeFlat:
					return PlungerDesc.GetFlat();
				case PlungerType.PlungerTypeCustom:
					return PlungerDesc.GetCustom(_data, beginy, springMinSpacing,
						out rody, out springGauge, out springRadius,
						out springLoops, out springEndLoops);
			}
			Logger.Warn("Unknown plunger type {0}.", _data.Type);
			return PlungerDesc.GetModern();
		}

		private void CalculateRenderDetails()
		{
			// get the number of lathe points from the descriptor
			lathePoints = desc.n;

			if (_data.Type == PlungerType.PlungerTypeFlat) {
				// For the flat plunger, we render every frame as a simple
				// flat rectangle.  This requires four vertices for the corners,
				// and two triangles -> 6 indices.
				vtsPerFrame = 4;
				indicesPerFrame = 6;

			} else {

				// For all other plungers, we render one circle per lathe
				// point.  Each circle has 'circlePoints' vertices.  We
				// also need to render the spring:  this consists of 3
				// spirals, where each spiral has 'springLoops' loops
				// times 'circlePoints' vertices.
				var latheVts = lathePoints * circlePoints;
				var springVts = (int)((springLoops + springEndLoops) * circlePoints) * 3;
				vtsPerFrame = latheVts + springVts;

				// For the lathed section, we need two triangles == 6
				// indices for every point on every lathe circle past
				// the first.  (We connect pairs of lathe circles, so
				// the first one doesn't count: two circles -> one set
				// of triangles, three circles -> two sets, etc).
				var latheIndices = 6 * circlePoints * (lathePoints - 1);

				// For the spring, we need 4 triangles == 12 indices
				// for every matching set of three vertices on the
				// three spirals, not counting the first set (as above,
				// we're connecting adjacent sets, so the first doesn't
				// count).  We already counted the total number of
				// vertices, so divide that by 3 to get the number
				// of sets.  12*vts/3 = 4*vts.
				//
				// The spring only applies to the custom plunger.
				var springIndices = 0;
				if (_data.Type == PlungerType.PlungerTypeCustom) {
					if ((springIndices = 4 * springVts - 12) < 0) {
						springIndices = 0;
					}
				}

				// the total number of indices is simply the sum of the
				// lathe and spring indices
				indicesPerFrame = latheIndices + springIndices;
			}
		}
	}
}
