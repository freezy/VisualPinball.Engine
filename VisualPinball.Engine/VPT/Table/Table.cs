using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;
using Logger = NLog.Logger;

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
		public ITableResourceContainer<Texture> Textures = new DefaultTableResourceContainer<Texture>();
		public readonly Dictionary<string, Sound.Sound> Sounds = new Dictionary<string, Sound.Sound>();
		public readonly Dictionary<string, Collection.Collection> Collections = new Dictionary<string, Collection.Collection>();

		#region GameItems

		private readonly Dictionary<string, Bumper.Bumper> _bumpers = new Dictionary<string, Bumper.Bumper>();
		private readonly List<Decal.Decal> _decals = new List<Decal.Decal>();
		private readonly Dictionary<string, DispReel.DispReel> _dispReels = new Dictionary<string, DispReel.DispReel>();
		private readonly Dictionary<string, Flipper.Flipper> _flippers = new Dictionary<string, Flipper.Flipper>();
		private readonly Dictionary<string, Gate.Gate> _gates = new Dictionary<string, Gate.Gate>();
		private readonly Dictionary<string, HitTarget.HitTarget> _hitTargets = new Dictionary<string, HitTarget.HitTarget>();
		private readonly Dictionary<string, Kicker.Kicker> _kickers = new Dictionary<string, Kicker.Kicker>();
		private readonly Dictionary<string, Light.Light> _lights = new Dictionary<string, Light.Light>();
		private readonly Dictionary<string, LightSeq.LightSeq> _lightSeqs = new Dictionary<string, LightSeq.LightSeq>();
		private readonly Dictionary<string, Plunger.Plunger> _plungers = new Dictionary<string, Plunger.Plunger>();
		private readonly Dictionary<string, Flasher.Flasher> _flashers = new Dictionary<string, Flasher.Flasher>();
		private readonly Dictionary<string, Primitive.Primitive> _primitives = new Dictionary<string, Primitive.Primitive>();
		private readonly Dictionary<string, Ramp.Ramp> _ramps = new Dictionary<string, Ramp.Ramp>();
		private readonly Dictionary<string, Rubber.Rubber> _rubbers = new Dictionary<string, Rubber.Rubber>();
		private readonly Dictionary<string, Spinner.Spinner> _spinners = new Dictionary<string, Spinner.Spinner>();
		private readonly Dictionary<string, Surface.Surface> _surfaces = new Dictionary<string, Surface.Surface>();
		private readonly Dictionary<string, TextBox.TextBox> _textBoxes = new Dictionary<string, TextBox.TextBox>();
		private readonly Dictionary<string, Timer.Timer> _timers = new Dictionary<string, Timer.Timer>();
		private readonly Dictionary<string, Trigger.Trigger> _triggers = new Dictionary<string, Trigger.Trigger>();

		public Bumper.Bumper Bumper(string name) => _bumpers[name];
		public Decal.Decal Decal(int i) => _decals[i];
		public DispReel.DispReel DispReel(string name) => _dispReels[name];
		public Flipper.Flipper Flipper(string name) => _flippers[name];
		public Gate.Gate Gate(string name) => _gates[name];
		public HitTarget.HitTarget HitTarget(string name) => _hitTargets[name];
		public Kicker.Kicker Kicker(string name) => _kickers[name];
		public Light.Light Light(string name) => _lights[name];
		public LightSeq.LightSeq LightSeq(string name) => _lightSeqs[name];
		public Plunger.Plunger Plunger(string name) => _plungers[name];
		public Flasher.Flasher Flasher(string name) => _flashers[name];
		public Primitive.Primitive Primitive(string name) => _primitives[name];
		public Ramp.Ramp Ramp(string name) => _ramps[name];
		public Rubber.Rubber Rubber(string name) => _rubbers[name];
		public Spinner.Spinner Spinner(string name) => _spinners[name];
		public Surface.Surface Surface(string name) => _surfaces[name];
		public TextBox.TextBox TextBox(string name) => _textBoxes[name];
		public Timer.Timer Timer(string name) => _timers[name];
		public Trigger.Trigger Trigger(string name) => _triggers[name];

		public void Add(Bumper.Bumper item) => AddItem(item.Name, item, _bumpers);
		public void Add(Decal.Decal item) => AddItem(item, _decals);
		public void Add(DispReel.DispReel item) => AddItem(item.Name, item, _dispReels);
		public void Add(Flipper.Flipper item) => AddItem(item.Name, item, _flippers);
		public void Add(Gate.Gate item) => AddItem(item.Name, item, _gates);
		public void Add(HitTarget.HitTarget item) => AddItem(item.Name, item, _hitTargets);
		public void Add(Kicker.Kicker item) => AddItem(item.Name, item, _kickers);
		public void Add(Light.Light item) => AddItem(item.Name, item, _lights);
		public void Add(LightSeq.LightSeq item) => AddItem(item.Name, item, _lightSeqs);
		public void Add(Plunger.Plunger item) => AddItem(item.Name, item, _plungers);
		public void Add(Flasher.Flasher item) => AddItem(item.Name, item, _flashers);
		public void Add(Primitive.Primitive item) => AddItem(item.Name, item, _primitives);
		public void Add(Ramp.Ramp item) => AddItem(item.Name, item, _ramps);
		public void Add(Rubber.Rubber item) => AddItem(item.Name, item, _rubbers);
		public void Add(Spinner.Spinner item) => AddItem(item.Name, item, _spinners);
		public void Add(Surface.Surface item) => AddItem(item.Name, item, _surfaces);
		public void Add(TextBox.TextBox item) => AddItem(item.Name, item, _textBoxes);
		public void Add(Timer.Timer item) => AddItem(item.Name, item, _timers);
		public void Add(Trigger.Trigger item) => AddItem(item.Name, item, _triggers);

		public IEnumerable<IRenderable> Renderables => new IRenderable[] { this }
			.Concat(_bumpers.Values)
			.Concat(_flippers.Values)
			.Concat(_gates.Values)
			.Concat(_hitTargets.Values)
			.Concat(_kickers.Values)
			.Concat(_lights.Values)
			.Concat(_plungers.Values)
			.Concat(_primitives.Values)
			.Concat(_ramps.Values)
			.Concat(_rubbers.Values)
			.Concat(_spinners.Values)
			.Concat(_surfaces.Values)
			.Concat(_triggers.Values);

		public IEnumerable<ItemData> GameItems => new ItemData[] {}
			.Concat(_bumpers.Values.Select(i => i.Data))
			.Concat(_decals.Select(i => i.Data))
			.Concat(_dispReels.Values.Select(i => i.Data))
			.Concat(_flippers.Values.Select(i => i.Data))
			.Concat(_flashers.Values.Select(i => i.Data))
			.Concat(_gates.Values.Select(i => i.Data))
			.Concat(_hitTargets.Values.Select(i => i.Data))
			.Concat(_kickers.Values.Select(i => i.Data))
			.Concat(_lights.Values.Select(i => i.Data))
			.Concat(_lightSeqs.Values.Select(i => i.Data))
			.Concat(_plungers.Values.Select(i => i.Data))
			.Concat(_primitives.Values.Select(i => i.Data))
			.Concat(_ramps.Values.Select(i => i.Data))
			.Concat(_rubbers.Values.Select(i => i.Data))
			.Concat(_spinners.Values.Select(i => i.Data))
			.Concat(_surfaces.Values.Select(i => i.Data))
			.Concat(_textBoxes.Values.Select(i => i.Data))
			.Concat(_timers.Values.Select(i => i.Data))
			.Concat(_triggers.Values.Select(i => i.Data));

		public int NumGameItems =>
			_bumpers.Count +
			_decals.Count +
			_dispReels.Count +
			_flippers.Count +
			_flashers.Count +
			_gates.Count +
			_hitTargets.Count +
			_kickers.Count +
			_lights.Count +
			_lightSeqs.Count +
			_plungers.Count +
			_primitives.Count +
			_ramps.Count +
			_rubbers.Count +
			_spinners.Count +
			_surfaces.Count +
			_textBoxes.Count +
			_timers.Count +
			_triggers.Count;

		public IEnumerable<IMovable> Movables => new IMovable[0]
			.Concat(_flippers.Values)
			.Concat(_gates.Values)
			.Concat(_spinners.Values);

		public IEnumerable<IHittable> Hittables => new IHittable[0]
			.Concat(_bumpers.Values)
			.Concat(_flippers.Values)
			.Concat(_gates.Values)
			.Concat(_hitTargets.Values)
			.Concat(_kickers.Values)
			.Concat(_plungers.Values)
			.Concat(_ramps.Values)
			.Concat(_rubbers.Values)
			.Concat(_spinners.Values)
			.Concat(_surfaces.Values)
			.Concat(_triggers.Values);

		public IEnumerable<IPlayable> Playables => new IPlayable[0]
			.Concat(_bumpers.Values)
			.Concat(_flippers.Values)
			.Concat(_gates.Values)
			.Concat(_hitTargets.Values)
			.Concat(_kickers.Values)
			.Concat(_plungers.Values)
			.Concat(_ramps.Values)
			.Concat(_rubbers.Values)
			.Concat(_spinners.Values)
			.Concat(_surfaces.Values)
			.Concat(_triggers.Values);

		private static void AddItem<TItem>(string name, TItem item, IDictionary<string, TItem> d)
		{
			d[name] = item;
		}

		private static void AddItem<TItem>(TItem item, ICollection<TItem> d)
		{
			d.Add(item);
		}

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
			_hitGenerator = new TableHitGenerator(this);
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

		public void SetTextureContainer(ITableResourceContainer<Texture> container)
		{
			Textures = container;
		}

		public Texture GetTexture(string name)
		{
			string lowerName = name.ToLower();
			var tex = name == null
				? null
				: Textures[lowerName];
			return tex;
		}

		public float GetScaleZ()
		{
			return Data.BgScaleZ?[Data.BgCurrentSet] ?? 1.0f;
		}

		public void SetupPlayfieldMesh()
		{
			if (_primitives.ContainsKey("playfield_mesh")) {
				_meshGenerator.SetFromPrimitive(_primitives["playfield_mesh"]);
				_primitives.Remove("playfield_mesh");
			}
		}

		public float GetSurfaceHeight(string surfaceName, float x, float y)
		{
			if (string.IsNullOrEmpty(surfaceName)) {
				return Data.TableHeight;
			}

			if (_surfaces.ContainsKey(surfaceName)) {
				return Data.TableHeight + _surfaces[surfaceName].Data.HeightTop;
			}

			if (_ramps.ContainsKey(surfaceName)) {
				return Data.TableHeight + _ramps[surfaceName].GetSurfaceHeight(x, y, this);
			}

			Logger.Warn(
				"[Table.getSurfaceHeight] Unknown surface {0}.\nAvailable surfaces: [ {1} ]\nAvailable ramps: [ {2} ]",
				surfaceName,
				string.Join(", ", _surfaces.Keys),
				string.Join(", ", _ramps.Keys)
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

