﻿// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
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
using VisualPinball.Unity.Extensions;
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
	[AddComponentMenu("Visual Pinball/Table")]
	public class TableBehavior : ItemBehavior<Engine.VPT.Table.Table, TableData>
	{
		public Engine.VPT.Table.Table Table => Item;
		public TableSerializedTexture[] Textures => _sidecar?.textures;
		public Patcher.Patcher.Patcher Patcher { get; internal set; }

		protected override string[] Children => null;

		[HideInInspector] [SerializeField] public string physicsEngineId;
		[HideInInspector] [SerializeField] public string debugUiId;
		[HideInInspector] [SerializeField] private TableSidecar _sidecar;
		private readonly Dictionary<string, Texture2D> _unityTextures = new Dictionary<string, Texture2D>();
		// note: this cache needs to be keyed on the engine material itself so that when its recreated due to property changes the unity material
		// will cache miss and get recreated as well
		private readonly Dictionary<PbrMaterial, UnityEngine.Material> _unityMaterials = new Dictionary<PbrMaterial, UnityEngine.Material>();
		// keep a list of texture names that need recreation, serialized and lazy so when undo happens they'll be considered dirty again
		[SerializeField] private List<string> _dirtyTextures = new List<string>();

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

		protected virtual void Start()
		{
			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().Init(this);
			}
		}

		protected override void OnDrawGizmos()
		{
			// do nothing, base class draws all child meshes for ease of selection, but
			// that would just be everything at this level
		}

		protected override Engine.VPT.Table.Table GetItem()
		{
			return RecreateTable();
		}

		internal TableSidecar GetOrCreateSidecar()
		{
			if (_sidecar == null) {
				var sidecarGo = new GameObject("Table Sidecar");
				sidecarGo.transform.parent = this.transform;
				_sidecar = sidecarGo.AddComponent<TableSidecar>();
			}
			return _sidecar;
		}

		public void AddTexture(string name, Texture2D texture)
		{
			_unityTextures[name.ToLower()] = texture;
		}

		public void MarkTextureDirty(string name)
		{
			_dirtyTextures.Add(name.ToLower());
		}

		public Texture2D GetTexture(string name)
		{
			var lowerName = name.ToLower();
			bool forceRecreate = false;
			if (_dirtyTextures.Contains(lowerName)) {
				forceRecreate = true;
				_dirtyTextures.Remove(lowerName);
			}
			if (!forceRecreate && _unityTextures.ContainsKey(lowerName)) {
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

		public void AddMaterial(PbrMaterial vpxMat, UnityEngine.Material material)
		{
			UnityEngine.Material oldMaterial = null;
			_unityMaterials.TryGetValue(vpxMat, out oldMaterial);

			_unityMaterials[vpxMat] = material;
			if (oldMaterial != null) {
				Destroy(oldMaterial);
			}
		}

		public UnityEngine.Material GetMaterial(PbrMaterial vpxMat)
		{
			if (_unityMaterials.ContainsKey(vpxMat)) {
				return _unityMaterials[vpxMat];
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
			foreach (var k in _sidecar.tableInfo.Keys) {
				table.TableInfo[k] = _sidecar.tableInfo[k];
			}

			// restore custom info tags
			table.CustomInfoTags = _sidecar.customInfoTags;

			// restore game items with no game object (yet!)
			table.Decals.Clear();
			table.Decals.AddRange(_sidecar.decals.Select(d => new Decal(d)));
			Restore(_sidecar.collections, table.Collections, d => new Collection(d));
			Restore(_sidecar.dispReels, table.DispReels, d => new DispReel(d));
			Restore(_sidecar.flashers, table.Flashers, d => new Flasher(d));
			Restore(_sidecar.lightSeqs, table.LightSeqs, d => new LightSeq(d));
			Restore(_sidecar.plungers, table.Plungers, d => new Engine.VPT.Plunger.Plunger(d));
			Restore(_sidecar.textBoxes, table.TextBoxes, d => new TextBox(d));
			Restore(_sidecar.timers, table.Timers, d => new Timer(d));

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

			Restore(_sidecar.sounds, table.Sounds, d => new Sound(d));

			// restore textures
			Logger.Info("Restoring textures...");
			foreach (var textureData in _sidecar.textures) {
				var texture = new Texture(textureData.Data);
				table.Textures[texture.Name.ToLower()] = texture;
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
