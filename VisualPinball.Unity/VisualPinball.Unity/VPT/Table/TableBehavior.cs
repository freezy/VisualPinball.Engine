// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using UnityEngine;
using UnityEngine.UI;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.Common;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Import;
using VisualPinball.Unity.Physics.DebugUI;
using VisualPinball.Unity.Physics.Engine;
using VisualPinball.Unity.VPT.Bumper;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Gate;
using VisualPinball.Unity.VPT.HitTarget;
using VisualPinball.Unity.VPT.Kicker;
using VisualPinball.Unity.VPT.Light;
using VisualPinball.Unity.VPT.Plunger;
using VisualPinball.Unity.VPT.Primitive;
using VisualPinball.Unity.VPT.Ramp;
using VisualPinball.Unity.VPT.Rubber;
using VisualPinball.Unity.VPT.Spinner;
using VisualPinball.Unity.VPT.Surface;
using VisualPinball.Unity.VPT.Trigger;
using Logger = NLog.Logger;
using SurfaceData = VisualPinball.Engine.VPT.Surface.SurfaceData;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.VPT.Table
{
	public interface ITextureStore
	{
		void AddTexture(string name, Texture2D texture);
		Texture2D GetTexture(string name);
	}

	[AddComponentMenu("Visual Pinball/Table")]
	public class TableBehavior : ItemBehavior<Engine.VPT.Table.Table, TableData>, ITextureStore
	{
		public Engine.VPT.Table.Table Table => Item;

		[HideInInspector] public Dictionary<string, string> tableInfo = new SerializableDictionary<string, string>();
		[HideInInspector] public TextureData[] textures;
		[HideInInspector] public CustomInfoTags customInfoTags;
		[HideInInspector] public CollectionData[] collections;
		[HideInInspector] public DecalData[] decals;
		[HideInInspector] public DispReelData[] dispReels;
		[HideInInspector] public FlasherData[] flashers;
		[HideInInspector] public LightSeqData[] lightSeqs;
		[HideInInspector] public PlungerData[] plungers;
		[HideInInspector] public SoundData[] sounds;
		[HideInInspector] public TextBoxData[] textBoxes;
		[HideInInspector] public TimerData[] timers;

		[HideInInspector] public string physicsEngineId;
		[HideInInspector] public string debugUiId;

		protected override string[] Children => null;

		private Dictionary<string, Texture2D> _unityTextures = new Dictionary<string, Texture2D>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void Awake()
		{
			base.Awake();
			EngineProvider<IPhysicsEngine>.Set(physicsEngineId);
			EngineProvider<IPhysicsEngine>.Get().Init(this);
			if (!string.IsNullOrEmpty(debugUiId)) {
				EngineProvider<IDebugUI>.Set(debugUiId);
			}
		}

		private void Start()
		{
			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().Init(this);
			}
		}

		protected override Engine.VPT.Table.Table GetItem()
		{
			return RecreateTable();
		}

		public void AddTexture(string name, Texture2D texture)
		{
			_unityTextures[name.ToLower()] = texture;
		}

		public Texture2D GetTexture(string name)
		{
			var lowerName = name.ToLower();
			if (_unityTextures.ContainsKey(lowerName)) {
				return _unityTextures[lowerName];
			}
			var tableTex = Table.GetTexture(lowerName);
			if (tableTex != null) {
				var unityTex = tableTex.ToUnityTexture();
				_unityTextures[lowerName] = unityTex;
				return unityTex;
			}
			return null;
		}

		public Engine.VPT.Table.Table CreateTable()
		{
			Logger.Info("Restoring table...");
			// restore table data
			var table = new Engine.VPT.Table.Table(data);

			// restore table info
			Logger.Info("Restoring table info...");
			foreach (var k in tableInfo.Keys) {
				table.TableInfo[k] = tableInfo[k];
			}

			// restore custom info tags
			table.CustomInfoTags = customInfoTags;

			// restore game items with no game object (yet!)
			table.Decals.Clear();
			table.Decals.AddRange(decals.Select(d => new Decal(d)));
			Restore(collections, table.Collections, d => new Collection(d));
			Restore(dispReels, table.DispReels, d => new DispReel(d));
			Restore(flashers, table.Flashers, d => new Flasher(d));
			Restore(lightSeqs, table.LightSeqs, d => new LightSeq(d));
			Restore(plungers, table.Plungers, d => new Engine.VPT.Plunger.Plunger(d));
			Restore(textBoxes, table.TextBoxes, d => new TextBox(d));
			Restore(timers, table.Timers, d => new Timer(d));

			// restore game items
			Logger.Info("Restoring game items...");
			Restore<BumperBehavior, Engine.VPT.Bumper.Bumper, BumperData>(table.Bumpers);
			Restore<FlipperBehavior, Engine.VPT.Flipper.Flipper, FlipperData>(table.Flippers);
			Restore<GateBehavior, Engine.VPT.Gate.Gate, GateData>(table.Gates);
			Restore<HitTargetBehavior, Engine.VPT.HitTarget.HitTarget, HitTargetData>(table.HitTargets);
			Restore<KickerBehavior, Engine.VPT.Kicker.Kicker, KickerData>(table.Kickers);
			Restore<LightBehavior, Engine.VPT.Light.Light, LightData>(table.Lights);
			Restore<PlungerBehavior, Engine.VPT.Plunger.Plunger, PlungerData>(table.Plungers);
			Restore<PrimitiveBehavior, Engine.VPT.Primitive.Primitive, PrimitiveData>(table.Primitives);
			Restore<RampBehavior, Engine.VPT.Ramp.Ramp, RampData>(table.Ramps);
			Restore<RubberBehavior, Engine.VPT.Rubber.Rubber, RubberData>(table.Rubbers);
			Restore<SpinnerBehavior, Engine.VPT.Spinner.Spinner, SpinnerData>(table.Spinners);
			Restore<SurfaceBehavior, Engine.VPT.Surface.Surface, SurfaceData>(table.Surfaces);
			Restore<TriggerBehavior, Engine.VPT.Trigger.Trigger, TriggerData>(table.Triggers);

			return table;
		}

		public Engine.VPT.Table.Table RecreateTable()
		{
			var table = CreateTable();

			Restore(sounds, table.Sounds, d => new Sound(d));

			// restore textures
			Logger.Info("Restoring textures...");
			foreach (var textureData in textures) {
				var texture = new Texture(textureData);
				table.Textures[texture.Name] = texture;
			}

			Logger.Info("Table restored.");
			return table;
		}

		private void Restore<TComp, TItem, TData>(IDictionary<string, TItem> dest) where TData : ItemData where TItem : Item<TData>, IRenderable where TComp : ItemBehavior<TItem, TData>
		{
			foreach (var component in GetComponentsInChildren<TComp>(true)) {
				dest[component.name] = component.Item;
			}
		}

		private static void Restore<TItem, TData>(IEnumerable<TData> src, IDictionary<string, TItem> dest, Func<TData, TItem> create) where TData : ItemData where TItem : Item<TData>
		{
			foreach (var d in src) {
				dest[d.GetName()] = create(d);
			}
		}
	}
}
