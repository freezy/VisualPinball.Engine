using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

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
		public CustomInfoTags CustomInfoTags { get; set; }
		public int FileVersion { get; set; }
		public byte[] FileHash { get; set; }

		public float Width => Data.Right - Data.Left;
		public float Height => Data.Bottom - Data.Top;
		public float TableHeight => Data.TableHeight;

		public bool HasMeshAsPlayfield => _meshGenerator.HasMeshAsPlayfield;

		public readonly Dictionary<string, string> TableInfo = new Dictionary<string, string>();
		public readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();
		public readonly Dictionary<string, Sound.Sound> Sounds = new Dictionary<string, Sound.Sound>();
		public readonly Dictionary<string, Collection.Collection> Collections = new Dictionary<string, Collection.Collection>();

		#region GameItems

		public readonly Dictionary<string, Bumper.Bumper> Bumpers = new Dictionary<string, Bumper.Bumper>();
		public readonly List<Decal.Decal> Decals = new List<Decal.Decal>();
		public readonly Dictionary<string, DispReel.DispReel> DispReels = new Dictionary<string, DispReel.DispReel>();
		public readonly Dictionary<string, Flipper.Flipper> Flippers = new Dictionary<string, Flipper.Flipper>();
		public readonly Dictionary<string, Gate.Gate> Gates = new Dictionary<string, Gate.Gate>();
		public readonly Dictionary<string, HitTarget.HitTarget> HitTargets = new Dictionary<string, HitTarget.HitTarget>();
		public readonly Dictionary<string, Kicker.Kicker> Kickers = new Dictionary<string, Kicker.Kicker>();
		public readonly Dictionary<string, Light.Light> Lights = new Dictionary<string, Light.Light>();
		public readonly Dictionary<string, LightSeq.LightSeq> LightSeqs = new Dictionary<string, LightSeq.LightSeq>();
		public readonly Dictionary<string, Plunger.Plunger> Plungers = new Dictionary<string, Plunger.Plunger>();
		public readonly Dictionary<string, Flasher.Flasher> Flashers = new Dictionary<string, Flasher.Flasher>();
		public readonly Dictionary<string, Primitive.Primitive> Primitives = new Dictionary<string, Primitive.Primitive>();
		public readonly Dictionary<string, Ramp.Ramp> Ramps = new Dictionary<string, Ramp.Ramp>();
		public readonly Dictionary<string, Rubber.Rubber> Rubbers = new Dictionary<string, Rubber.Rubber>();
		public readonly Dictionary<string, Spinner.Spinner> Spinners = new Dictionary<string, Spinner.Spinner>();
		public readonly Dictionary<string, Surface.Surface> Surfaces = new Dictionary<string, Surface.Surface>();
		public readonly Dictionary<string, TextBox.TextBox> TextBoxes = new Dictionary<string, TextBox.TextBox>();
		public readonly Dictionary<string, Timer.Timer> Timers = new Dictionary<string, Timer.Timer>();
		public readonly Dictionary<string, Trigger.Trigger> Triggers = new Dictionary<string, Trigger.Trigger>();

		public IEnumerable<IRenderable> Renderables => new IRenderable[] { this }
			.Concat(Bumpers.Values)
			.Concat(Flippers.Values)
			.Concat(Gates.Values)
			.Concat(HitTargets.Values)
			.Concat(Kickers.Values)
			.Concat(Lights.Values)
			.Concat(Primitives.Values)
			.Concat(Ramps.Values)
			.Concat(Rubbers.Values)
			.Concat(Spinners.Values)
			.Concat(Surfaces.Values)
			.Concat(Triggers.Values);

		public IEnumerable<ItemData> GameItems => new ItemData[] {}
			.Concat(Bumpers.Values.Select(i => i.Data))
			.Concat(Decals.Select(i => i.Data))
			.Concat(DispReels.Values.Select(i => i.Data))
			.Concat(Flippers.Values.Select(i => i.Data))
			.Concat(Flashers.Values.Select(i => i.Data))
			.Concat(Gates.Values.Select(i => i.Data))
			.Concat(HitTargets.Values.Select(i => i.Data))
			.Concat(Kickers.Values.Select(i => i.Data))
			.Concat(Lights.Values.Select(i => i.Data))
			.Concat(LightSeqs.Values.Select(i => i.Data))
			.Concat(Plungers.Values.Select(i => i.Data))
			.Concat(Primitives.Values.Select(i => i.Data))
			.Concat(Ramps.Values.Select(i => i.Data))
			.Concat(Rubbers.Values.Select(i => i.Data))
			.Concat(Spinners.Values.Select(i => i.Data))
			.Concat(Surfaces.Values.Select(i => i.Data))
			.Concat(TextBoxes.Values.Select(i => i.Data))
			.Concat(Timers.Values.Select(i => i.Data))
			.Concat(Triggers.Values.Select(i => i.Data));

		public IEnumerable<IMovable> Movables => new IMovable[0]
			.Concat(Flippers.Values);

		public IEnumerable<IHittable> Hittables => new IHittable[0]
			.Concat(Flippers.Values)
			.Concat(Surfaces.Values);

		public IEnumerable<IPlayable> Playables => new IPlayable[0]
			.Concat(Flippers.Values)
			.Concat(Surfaces.Values);

		#endregion

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

		private readonly TableMeshGenerator _meshGenerator;
		private readonly TableHitGenerator _hitGenerator;

		/// <summary>
		/// The API to load the table from a file.
		/// </summary>
		/// <param name="filename">Path to the VPX file</param>
		/// <param name="loadGameItems">If false, game items are not loaded. Useful when loading them on multiple threads.</param>
		/// <returns>The parsed table</returns>
		public static Table Load(string filename, bool loadGameItems = true)
		{
			return TableLoader.Load(filename, loadGameItems);
		}

		public Table(TableData data) : base(data)
		{
			_meshGenerator = new TableMeshGenerator(Data);
			_hitGenerator = new TableHitGenerator(Data);
		}

		public Table(BinaryReader reader) : this(new TableData(reader)) { }

		public RenderObjectGroup GetRenderObjects(Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public IEnumerable<HitObject> GetHitShapes() => _hitGenerator.GenerateHitObjects();
		public HitPlane GeneratePlayfieldHit() => _hitGenerator.GeneratePlayfieldHit();
		public HitPlane GenerateGlassHit() => _hitGenerator.GenerateGlassHit();

		public void Save(string fileName)
		{
			new TableWriter(this).WriteTable(fileName);
			Logger.Info("File successfully saved to {0}.", fileName);
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

		public void SetupPlayfieldMesh()
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

			if (Ramps.ContainsKey(surfaceName)) {
				return Data.TableHeight + Ramps[surfaceName].GetSurfaceHeight(x, y, this);
			}

			Logger.Warn(
				"[Table.getSurfaceHeight] Unknown surface {0}.\nAvailable surfaces: [ {1} ]\nAvailable ramps: [ {2} ]",
				surfaceName,
				string.Join(", ", Surfaces.Keys),
				string.Join(", ", Ramps.Keys)
			);
			return Data.TableHeight;
		}

		public int GetDetailLevel()
		{
			return 10; // TODO
		}

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
	}
}

