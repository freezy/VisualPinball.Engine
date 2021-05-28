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

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Table;
using Logger = NLog.Logger;
using Material = UnityEngine.Material;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Table")]
	public class TableAuthoring : ItemMainRenderableAuthoring<Table, TableData>
	{
		protected override Table InstantiateItem(TableData data) => new Table(TableHolder, data);

		protected override Type MeshAuthoringType { get; } = null;
		protected override Type ColliderAuthoringType { get; } = null;

		public override IEnumerable<Type> ValidParents => new Type[0];
		public Table Table => Item;
		public List<CollectionData> Collections => _sidecar?.collections;
		public MappingsData Mappings => _sidecar?.mappings;

		public ITableHolder TableHolder => _ta ??= new SceneTableHolder(this);

		//public PatcherManager.Patcher Patcher { get; internal set; }

		[HideInInspector] [SerializeField] public string physicsEngineId = "VisualPinball.Unity.DefaultPhysicsEngine";
		[HideInInspector] [SerializeField] public string debugUiId;
		[HideInInspector] [SerializeField] private TableSidecar _sidecar;
		private readonly Dictionary<string, Texture2D> _unityTextures = new Dictionary<string, Texture2D>();
		// note: this cache needs to be keyed on the engine material itself so that when its recreated due to property changes the unity material
		// will cache miss and get recreated as well
		private readonly Dictionary<string, Material> _unityMaterials = new Dictionary<string, Material>();
		/// <summary>
		/// Keeps a list of serializables names that need recreation, serialized and
		/// lazy so when undo happens they'll be considered dirty again
		/// </summary>
		[HideInInspector] [SerializeField] private Dictionary<Type, List<string>> _dirtySerializables = new Dictionary<Type, List<string>>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private SceneTableHolder _ta;

		//Private runtime values needed for camera adjustments.  
		[HideInInspector] [SerializeField] public  Bounds _tableBounds;
		[HideInInspector] [SerializeField] public  Vector3 _tableCenter;

		public void Awake()
		{
			//Store table information 
			_tableBounds = GetTableBounds();
			_tableCenter = GetTableCenter();
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

		public TableSidecar GetOrCreateSidecar()
		{
			if (_sidecar == null) {
				_sidecar = ScriptableObject.CreateInstance<TableSidecar>();
			}
			return _sidecar;
		}

		public void AddTexture(string name, Texture2D texture)
		{
			_unityTextures[name.ToLower()] = texture;
		}

		public void RestoreCollections(List<CollectionData> collections)
		{
			Collections.Clear();
			Collections.AddRange(collections);
		}

		public void RestoreMappings(MappingsData mappings)
		{
			Mappings.Coils = mappings.Coils.ToArray();
			Mappings.Switches = mappings.Switches.ToArray();
			Mappings.Wires = mappings.Wires.ToArray();
		}

		public void MarkDirty<T>(string name) where T : IItem
		{
			if (!_dirtySerializables.ContainsKey(typeof(T))) {
				_dirtySerializables[typeof(T)] = new List<string>();
			}
			_dirtySerializables[typeof(T)].Add(name.ToLower());
		}

		private bool CheckDirty<T>(string name, Func<bool> action = null) where T : IItem
		{
			List<string> lst;
			if (_dirtySerializables.TryGetValue(typeof(T), out lst)) {
				if (lst.Contains(name)) {
					var remove = true;
					if (action != null) {
						remove = action.Invoke();
					}
					if (remove) {
						_dirtySerializables[typeof(T)].Remove(name);
					}
					return true;
				}
			}

			return false;
		}

		public override void Restore()
		{
			// update the name
			Item.Name = name;
		}

		public Texture2D GetTexture(string name)
		{
			var lowerName = name.ToLower();
			// check to see if the texture we're after has been flagged as dirty and thus needs to be recreated from table data
			var forceRecreate = CheckDirty<Texture>(lowerName);

			// don't need to recreate it, and we have the texture in cache
			if (!forceRecreate && _unityTextures.ContainsKey(lowerName)) {
				return _unityTextures[lowerName];
			}
			// create unity texture from vpe data and put in a cache for future retrievals
			var tableTex = Table.GetTexture(lowerName);
			if (tableTex != null) {
				var unityTex = tableTex.ToUnityTexture();
				_unityTextures[lowerName] = unityTex;
				return unityTex;
			}
			return null;
		}

		public void AddMaterial(PbrMaterial vpxMat, Material material)
		{
			_unityMaterials.TryGetValue(vpxMat.Id, out var oldMaterial);
			_unityMaterials[vpxMat.Id] = material;
			if (oldMaterial != null) {
				Destroy(oldMaterial);
			}
		}

		public Material GetMaterial(PbrMaterial vpxMat)
		{
			if (_unityMaterials.ContainsKey(vpxMat.Id)) {
				return _unityMaterials[vpxMat.Id];
			}
			return null;
		}

		// public Table CreateTable(TableData data)
		// {
		// 	Logger.Info("Restoring table...");
		// 	// restore table data
		// 	var table = new Table(data);
		//
		// 	// restore table info
		// 	Logger.Info("Restoring table info...");
		// 	foreach (var k in _sidecar.tableInfo.Keys) {
		// 		table.TableInfo[k] = _sidecar.tableInfo[k];
		// 	}
		//
		// 	// restore custom info tags
		// 	table.CustomInfoTags = _sidecar.customInfoTags;
		//
		// 	// restore custom info tags
		// 	table.Mappings = new Mappings(_sidecar.mappings);
		//
		// 	// restore game items with no game object (yet!)
		// 	table.ReplaceAll(_sidecar.decals.Select(d => new Decal(d)));
		// 	Restore(_sidecar.collections, table.Collections, d => new Collection(d));
		// 	Restore(_sidecar.dispReels, table, d => new DispReel(d));
		// 	Restore(_sidecar.flashers, table, d => new Flasher(d));
		// 	Restore(_sidecar.lightSeqs, table, d => new LightSeq(d));
		// 	Restore(_sidecar.textBoxes, table, d => new TextBox(d));
		// 	Restore(_sidecar.timers, table, d => new Timer(d));
		//
		// 	// restore game items
		// 	Logger.Info("Restoring game items...");
		// 	Restore<BumperAuthoring, Bumper, BumperData>(table);
		// 	Restore<FlipperAuthoring, Flipper, FlipperData>(table);
		// 	Restore<GateAuthoring, Gate, GateData>(table);
		// 	Restore<HitTargetAuthoring, HitTarget, HitTargetData>(table);
		// 	Restore<KickerAuthoring, Kicker, KickerData>(table);
		// 	Restore<LightAuthoring, Light, LightData>(table);
		// 	Restore<PlungerAuthoring, Plunger, PlungerData>(table);
		// 	Restore<PrimitiveAuthoring, Primitive, PrimitiveData>(table);
		// 	Restore<RampAuthoring, Ramp, RampData>(table);
		// 	Restore<RubberAuthoring, Rubber, RubberData>(table);
		// 	Restore<SpinnerAuthoring, Spinner, SpinnerData>(table);
		// 	Restore<SurfaceAuthoring, Surface, SurfaceData>(table);
		// 	Restore<TriggerAuthoring, Trigger, TriggerData>(table);
		// 	Restore<TroughAuthoring, Trough, TroughData>(table);
		//
		// 	return table;
		// }

		// public Table RecreateTable(TableData tableData)
		// {
		// 	var table = CreateTable(tableData);
		//
		// 	Logger.Info("Table restored.");
		// 	return table;
		// }

		public Vector3 GetTableCenter()
		{
			var playfield = GetComponentInChildren<PlayfieldAuthoring>().gameObject;
			return playfield.GetComponent<MeshRenderer>().bounds.center;
		}

		public Bounds GetTableBounds()
		{

			var tableBounds = new Bounds();

			var mrs = GetComponentsInChildren<Renderer>();
			foreach(var mr in mrs) 
			{
				tableBounds.Encapsulate(mr.bounds);
			}

			return tableBounds;
		}

		public void RepopulateHardware(IGamelogicEngine gle)
		{
			Mappings.RemoveAllSwitches();
			TableHolder.Mappings.PopulateSwitches(gle.AvailableSwitches, TableHolder.Switchables, TableHolder.SwitchableDevices);

			Mappings.RemoveAllCoils();
			TableHolder.Mappings.PopulateCoils(gle.AvailableCoils, TableHolder.Coilables, TableHolder.CoilableDevices);

			Mappings.RemoveAllLamps();
			TableHolder.Mappings.PopulateLamps(gle.AvailableLamps, TableHolder.Lightables);
		}

		// private void Restore<TComp, TItem, TData>(Table table) where TData : ItemData
		// 	where TItem : Item<TData>
		// 	where TComp : ItemMainAuthoring<TItem, TData>
		// {
		// 	foreach (var component in GetComponentsInChildren<TComp>(true))
		// 	{
		// 		component.Restore();
		// 		table.Add(component.Item);
		// 	}
		// }
		//
		// private static void Restore<TItem, TData>(IEnumerable<TData> src, IDictionary<string, TItem> dest, Func<TData, TItem> create) where TData : ItemData where TItem : Item<TData>
		// {
		// 	foreach (var d in src) {
		// 		dest[d.GetName()] = create(d);
		// 	}
		// }
		//
		// private static void Restore<TItem, TData>(IEnumerable<TData> src, Table table, Func<TData, TItem> create) where TData : ItemData where TItem : Item<TData>
		// {
		// 	foreach (var d in src) {
		// 		table.Add(create(d));
		// 	}
		// }

	}
}
