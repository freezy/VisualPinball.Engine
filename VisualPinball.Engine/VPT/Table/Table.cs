using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using VisualPinball.Engine.Game;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// The root object for everything table related. <p/>
	///
	/// A table contains all the playfield elements, as well as a set of
	/// global data.
	/// </summary>
	public class Table : Item<TableData>, IRenderable
	{
		public readonly Dictionary<string, string> TableInfo = new Dictionary<string, string>();
		public readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Bumper.Bumper> Bumpers = new Dictionary<string, VisualPinball.Engine.VPT.Bumper.Bumper>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Flipper.Flipper> Flippers = new Dictionary<string, VisualPinball.Engine.VPT.Flipper.Flipper>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.HitTarget.HitTarget> HitTargets = new Dictionary<string, VisualPinball.Engine.VPT.HitTarget.HitTarget>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Light.Light> Lights = new Dictionary<string, VisualPinball.Engine.VPT.Light.Light>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Primitive.Primitive> Primitives = new Dictionary<string, VisualPinball.Engine.VPT.Primitive.Primitive>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Ramp.Ramp> Ramps = new Dictionary<string, VisualPinball.Engine.VPT.Ramp.Ramp>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Rubber.Rubber> Rubbers = new Dictionary<string, VisualPinball.Engine.VPT.Rubber.Rubber>();
		public readonly Dictionary<string, VisualPinball.Engine.VPT.Surface.Surface> Surfaces = new Dictionary<string, VisualPinball.Engine.VPT.Surface.Surface>();

		#region Table Info
		public string InfoAuthorEmail => TableInfo.ContainsKey("AuthorEmail") ? TableInfo["AuthorEmail"] : null;
		public string InfoAuthorName => TableInfo.ContainsKey("AuthorName") ? TableInfo["AuthorName"] : null;
		public string InfoAuthorWebsite => TableInfo.ContainsKey("AuthorWebSite") ? TableInfo["AuthorWebSite"] : null;
		public string InfoReleaseDate => TableInfo.ContainsKey("ReleaseDate") ? TableInfo["ReleaseDate"] : null;
		public string InfoBlurb => TableInfo.ContainsKey("TableBlurb") ? TableInfo["TableBlurb"] : null;
		public string InfoDescription => TableInfo.ContainsKey("TableDescription") ? TableInfo["TableDescription"] : null;
		public string InfoName => TableInfo.ContainsKey("TableName") ? TableInfo["TableName"] : null;
		public string InfoRules => TableInfo.ContainsKey("TableRules") ? TableInfo["TableRules"] : null;
		public string InfoVersion => TableInfo.ContainsKey("TableVersion") ? TableInfo["TableVersion"] : null;
		#endregion

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public float Width => Data.Right - Data.Left;
		public float Height => Data.Bottom - Data.Top;
		public float TableHeight => Data.TableHeight;

		public IRenderable[] Renderables => new IRenderable[] { this }
			.Concat(Bumpers.Values)
			.Concat(Flippers.Values)
			.Concat(HitTargets.Values)
			.Concat(Surfaces.Values)
			//.Concat(Lights.Values)
			.Concat(Primitives.Values)
			.Concat(Ramps.Values)
			.Concat(Rubbers.Values)
			.ToArray();

		private readonly TableMeshGenerator _meshGenerator;

		/// <summary>
		/// The API to load the table from a file.
		/// </summary>
		/// <param name="filename">Path to the VPX file</param>
		/// <returns>The parsed table</returns>
		public static Table Load(string filename)
		{
			return TableLoader.Load(filename);
		}

		public Table(BinaryReader reader) : base(new TableData(reader))
		{
			_meshGenerator = new TableMeshGenerator(Data);
		}

		public RenderObjectGroup GetRenderObjects(Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public Material GetMaterial(string name)
		{
			return Data.Materials == null || name == null
				? null
				: Data.Materials.FirstOrDefault(m => m.Name == name);
		}

		public Texture GetTexture(string name)
		{
			return name == null
				? null
				: Textures.ContainsKey(name.ToLower()) ? Textures[name.ToLower()] : null;
		}

		public float GetScaleZ()
		{
			return Data.BgScaleZ?[Data.BgCurrentSet] ?? 1.0f;
		}

		internal void SetupPlayfieldMesh()
		{
			if (Primitives.ContainsKey("playfield_mesh")) {
				_meshGenerator.SetFromPrimitive(this, Primitives["playfield_mesh"]);
				Primitives.Remove("playfield_mesh");
			}
		}

		public float GetSurfaceHeight(string surfaceName, float x, float y)
		{
			if (string.IsNullOrEmpty(surfaceName)) {
				return Data.TableHeight;
			}

			if (Surfaces.ContainsKey(surfaceName)) {
				return Data.TableHeight + Surfaces[surfaceName].Data.HeightTop;
			}

			// if (RampData.ContainsKey(surfaceName)) {
			// 	return Data.TableHeight + RampData[surfaceName].GetSurfaceHeight(x, y, this);
			// }

			Logger.Warn($"[Table.getSurfaceHeight] Unknown surface {surfaceName}.");
			return Data.TableHeight;
		}

		public int GetDetailLevel()
		{
			return 10; // TODO
		}
	}
}

