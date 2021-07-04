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

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Collection;

namespace VisualPinball.Engine.VPT.Table
{
	public abstract class TableContainer
	{
		public abstract Table Table { get; }
		public abstract Dictionary<string, string> TableInfo { get; }
		public abstract List<CollectionData> Collections { get; }
		public abstract Mappings.Mappings Mappings { get; }
		public abstract CustomInfoTags CustomInfoTags { get; }
		public abstract Material GetMaterial(string name);
		public abstract Texture GetTexture(string name);

		public int FileVersion { get; set; }
		public byte[] FileHash { get; set; }

		public ITableResourceContainer<Texture> Textures { get; protected set; } = new DefaultTableResourceContainer<Texture>();
		public ITableResourceContainer<Sound.Sound> Sounds { get; protected set; } = new DefaultTableResourceContainer<Sound.Sound>();

		public bool HasTrough => _troughs.Count > 0;
		public int NumTextures => Table.Data.NumTextures;
		public int NumGameItems => Table.Data.NumGameItems;
		public int NumSounds => Table.Data.NumSounds;
		public int NumCollections => Table.Data.NumCollections;

		#region GameItems

		protected readonly Dictionary<string, Bumper.Bumper> _bumpers = new Dictionary<string, Bumper.Bumper>();
		protected readonly List<Decal.Decal> _decals = new List<Decal.Decal>();
		protected readonly Dictionary<string, DispReel.DispReel> _dispReels = new Dictionary<string, DispReel.DispReel>();
		protected readonly Dictionary<string, Flasher.Flasher> _flashers = new Dictionary<string, Flasher.Flasher>();
		protected readonly Dictionary<string, Flipper.Flipper> _flippers = new Dictionary<string, Flipper.Flipper>();
		protected readonly Dictionary<string, Gate.Gate> _gates = new Dictionary<string, Gate.Gate>();
		protected readonly Dictionary<string, HitTarget.HitTarget> _hitTargets = new Dictionary<string, HitTarget.HitTarget>();
		protected readonly Dictionary<string, Kicker.Kicker> _kickers = new Dictionary<string, Kicker.Kicker>();
		protected readonly Dictionary<string, Light.Light> _lights = new Dictionary<string, Light.Light>();
		protected readonly Dictionary<string, LightSeq.LightSeq> _lightSeqs = new Dictionary<string, LightSeq.LightSeq>();
		protected readonly Dictionary<string, Plunger.Plunger> _plungers = new Dictionary<string, Plunger.Plunger>();
		protected readonly Dictionary<string, Primitive.Primitive> _primitives = new Dictionary<string, Primitive.Primitive>();
		protected readonly Dictionary<string, Ramp.Ramp> _ramps = new Dictionary<string, Ramp.Ramp>();
		protected readonly Dictionary<string, Rubber.Rubber> _rubbers = new Dictionary<string, Rubber.Rubber>();
		protected readonly Dictionary<string, Spinner.Spinner> _spinners = new Dictionary<string, Spinner.Spinner>();
		protected readonly Dictionary<string, Surface.Surface> _surfaces = new Dictionary<string, Surface.Surface>();
		protected readonly Dictionary<string, TextBox.TextBox> _textBoxes = new Dictionary<string, TextBox.TextBox>();
		protected readonly Dictionary<string, Timer.Timer> _timers = new Dictionary<string, Timer.Timer>();
		protected readonly Dictionary<string, Trigger.Trigger> _triggers = new Dictionary<string, Trigger.Trigger>();
		protected readonly Dictionary<string, Trough.Trough> _troughs = new Dictionary<string, Trough.Trough>();

		protected virtual void Clear()
		{
			_bumpers.Clear();
			_decals.Clear();
			_dispReels.Clear();
			_flashers.Clear();
			_flippers.Clear();
			_gates.Clear();
			_hitTargets.Clear();
			_kickers.Clear();
			_lights.Clear();
			_lightSeqs.Clear();
			_plungers.Clear();
			_primitives.Clear();
			_ramps.Clear();
			_rubbers.Clear();
			_spinners.Clear();
			_surfaces.Clear();
			_textBoxes.Clear();
			_timers.Clear();
			_triggers.Clear();
			_troughs.Clear();
		}

		public Bumper.Bumper Bumper(string name) => _bumpers[name];
		public Decal.Decal Decal(int i) => _decals[i];
		public DispReel.DispReel DispReel(string name) => _dispReels[name];
		public Flipper.Flipper Flipper(string name) => _flippers[name];
		public Gate.Gate Gate(string name) => _gates[name];
		public HitTarget.HitTarget HitTarget(string name) => _hitTargets[name];
		public Kicker.Kicker Kicker(string name) => _kickers[name];
		public Light.Light Light(string name) => _lights[name];
		public LightSeq.LightSeq LightSeq(string name) => _lightSeqs[name];
		public Plunger.Plunger Plunger(string name = null) => name == null ? _plungers.Values.FirstOrDefault() : _plungers[name];
		public Flasher.Flasher Flasher(string name) => _flashers[name];
		public Primitive.Primitive Primitive(string name) => _primitives[name];
		public Ramp.Ramp Ramp(string name) => _ramps[name];
		public Rubber.Rubber Rubber(string name) => _rubbers[name];
		public Spinner.Spinner Spinner(string name) => _spinners[name];
		public Surface.Surface Surface(string name) => _surfaces[name];
		public TextBox.TextBox TextBox(string name) => _textBoxes[name];
		public Timer.Timer Timer(string name) => _timers[name];
		public Trigger.Trigger Trigger(string name) => _triggers[name];
		public Trough.Trough Trough(string name) => _troughs[name];

		public IEnumerable<IRenderable> Renderables => new IRenderable[] { Table }
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

		/// <summary>
		/// Game items that need to be converted but aren't rendered.
		/// </summary>
		public IEnumerable<IItem> NonRenderables => new IItem[0]
			.Concat(_troughs.Values);

		public IEnumerable<IItem> GameItems => new IItem[] { }
			.Concat(_bumpers.Values)
			.Concat(_decals.Select(i => i))
			.Concat(_dispReels.Values)
			.Concat(_flippers.Values)
			.Concat(_flashers.Values)
			.Concat(_gates.Values)
			.Concat(_hitTargets.Values)
			.Concat(_kickers.Values)
			.Concat(_lights.Values)
			.Concat(_lightSeqs.Values)
			.Concat(_plungers.Values)
			.Concat(_primitives.Values)
			.Concat(_ramps.Values)
			.Concat(_rubbers.Values)
			.Concat(_spinners.Values)
			.Concat(_surfaces.Values)
			.Concat(_textBoxes.Values)
			.Concat(_timers.Values)
			.Concat(_triggers.Values)
			.Concat(_troughs.Values);

		public IEnumerable<ItemData> ItemDatas => new ItemData[] { }
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
			.Concat(_triggers.Values.Select(i => i.Data))
			.Concat(_troughs.Values.Select(i => i.Data));

		public IEnumerable<ISwitchable> Switchables => new ISwitchable[0]
			.Concat(_bumpers.Values)
			.Concat(_flippers.Values)
			.Concat(_gates.Values)
			.Concat(_hitTargets.Values)
			.Concat(_kickers.Values)
			.Concat(_spinners.Values)
			.Concat(_triggers.Values);

		public IEnumerable<ISwitchableDevice> SwitchableDevices => new ISwitchableDevice[0]
			.Concat(_troughs.Values);

		public IEnumerable<ICoilable> Coilables => new ICoilable[0]
			.Concat(_bumpers.Values)
			.Concat(_flippers.Values)
			.Concat(_kickers.Values);

		public IEnumerable<ICoilableDevice> CoilableDevices => new ICoilableDevice[0]
			.Concat(_troughs.Values)
			.Concat(_plungers.Values);

		public IEnumerable<ILightable> Lightables => new ILightable[0]
			.Concat(_lights.Values)
			.Concat(_flashers.Values);

		protected Dictionary<string, T> GetItemDictionary<T>() where T : IItem
		{
			return GetItemDictionary<T>(typeof(T));
		}

		protected Dictionary<string, T> GetItemDictionary<T>(Type t) where T : IItem
		{
			if (t == typeof(Bumper.Bumper)) {
				return _bumpers as Dictionary<string, T>;
			}
			if (t == typeof(DispReel.DispReel)) {
				return _dispReels as Dictionary<string, T>;
			}

			if (t == typeof(Flipper.Flipper)) {
				return _flippers as Dictionary<string, T>;
			}

			if (t == typeof(Gate.Gate)) {
				return _gates as Dictionary<string, T>;
			}

			if (t == typeof(HitTarget.HitTarget)) {
				return _hitTargets as Dictionary<string, T>;
			}

			if (t == typeof(Kicker.Kicker)) {
				return _kickers as Dictionary<string, T>;
			}

			if (t == typeof(Light.Light)) {
				return _lights as Dictionary<string, T>;
			}

			if (t == typeof(LightSeq.LightSeq)) {
				return _lightSeqs as Dictionary<string, T>;
			}

			if (t == typeof(Plunger.Plunger)) {
				return _plungers as Dictionary<string, T>;
			}

			if (t == typeof(Flasher.Flasher)) {
				return _flashers as Dictionary<string, T>;
			}

			if (t == typeof(Primitive.Primitive)) {
				return _primitives as Dictionary<string, T>;
			}

			if (t == typeof(Ramp.Ramp)) {
				return _ramps as Dictionary<string, T>;
			}

			if (t == typeof(Rubber.Rubber)) {
				return _rubbers as Dictionary<string, T>;
			}

			if (t == typeof(Spinner.Spinner)) {
				return _spinners as Dictionary<string, T>;
			}

			if (t == typeof(Surface.Surface)) {
				return _surfaces as Dictionary<string, T>;
			}

			if (t == typeof(TextBox.TextBox)) {
				return _textBoxes as Dictionary<string, T>;
			}

			if (t == typeof(Timer.Timer)) {
				return _timers as Dictionary<string, T>;
			}

			if (t == typeof(Trigger.Trigger)) {
				return _triggers as Dictionary<string, T>;
			}

			if (t == typeof(Trough.Trough)) {
				return _troughs as Dictionary<string, T>;
			}

			return null;
		}

		protected List<T> GetItemList<T>() {
			if (typeof(T) == typeof(Decal.Decal)) {
				return _decals as List<T>;
			}

			return null;
		}

		#endregion

		/// <summary>
		/// Checks whether a game item of a given type exists.
		/// </summary>
		/// <param name="name">Name of the game item</param>
		/// <typeparam name="T">Type of the game item</typeparam>
		/// <returns>True if the game item exists, false otherwise</returns>
		public bool Has<T>(string name) where T : IItem => GetItemDictionary<T>().ContainsKey(name);
		public T Get<T>(string name) where T : IItem => GetItemDictionary<T>()[name];

		/// <summary>
		/// Returns all game items of a given type.
		/// </summary>
		/// <typeparam name="TItem">Game item type</typeparam>
		/// <returns>All game items stored in the table</returns>
		/// <exception cref="ArgumentException">If invalid game type</exception>
		public TItem[] GetAll<TItem>() where TItem : IItem
		{
			var dict = GetItemDictionary<TItem>();
			if (dict != null) {
				return dict.Values.ToArray();
			}
			var list = GetItemList<TItem>();
			if (list != null) {
				return list.ToArray();
			}
			throw new ArgumentException("Unknown item type " + typeof(TItem) + ".");
		}

		public TData[] GetAllData<TItem, TData>() where TItem : Item<TData> where TData : ItemData
		{
			var dict = GetItemDictionary<TItem>();
			if (dict != null) {
				return dict.Values.Select(d => d.Data).ToArray();
			}
			var list = GetItemList<TItem>();
			if (list != null) {
				return list.Select(d => d.Data).ToArray();
			}
			throw new ArgumentException("Unknown item type " + typeof(TItem) + ".");
		}

		/// <summary>
		/// Removes a game item from the table.
		/// </summary>
		/// <param name="name">Name of the game item</param>
		/// <typeparam name="T">Type of the game item</typeparam>
		public void Remove<T>(string name) where T : IItem
		{
			var dict = GetItemDictionary<T>();
			if (!dict.ContainsKey(name)) {
				return;
			}
			var removedStorageIndex = dict[name].StorageIndex;
			var gameItems = ItemDatas;
			foreach (var gameItem in gameItems) {
				if (gameItem.StorageIndex > removedStorageIndex) {
					gameItem.StorageIndex--;
				}
			}

			Table.Data.NumGameItems = gameItems.Count() - 1;
			dict.Remove(name);
		}

		/// <summary>
		/// Computes a new name for a game item.
		/// </summary>
		/// <param name="prefix">Prefix</param>
		/// <typeparam name="T">Type of the game item</typeparam>
		/// <returns>New name, a concatenation of the prefix and the next free index</returns>
		public string GetNewName<T>(string prefix) where T : IItem
		{
			var n = 0;
			var dict = GetItemDictionary<T>();
			do {
				var elementName = $"{prefix}{++n}";
				if (!dict.ContainsKey(elementName)) {
					return elementName;
				}
			} while (true);
		}

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

		public virtual void Save(string fileName)
		{
			new TableWriter(this).WriteTable(fileName);
			Logger.Info("File successfully saved to {0}.", fileName);
		}

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

	}
}

