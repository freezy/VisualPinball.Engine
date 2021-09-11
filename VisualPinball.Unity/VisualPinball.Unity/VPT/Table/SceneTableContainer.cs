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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
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
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.Trough;
using Light = VisualPinball.Engine.VPT.Light.Light;
using Material = VisualPinball.Engine.VPT.Material;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity
{
	public class SceneTableContainer : TableContainer
	{
		public override Table Table => _table ??= new Table(_tableComponent.TableContainer, _tableComponent.LegacyContainer.TableData);
		public override Dictionary<string, string> TableInfo => _tableComponent.TableInfo;
		public override List<CollectionData> Collections => _tableComponent.Collections;
		[Obsolete("Use MappingConfig")]
		public override CustomInfoTags CustomInfoTags => _tableComponent.CustomInfoTags;

		public const int ChildObjectsLayer = 16;

		public override IEnumerable<Texture> Textures => _tableComponent.LegacyContainer.Textures
			.Where(texture => texture.IsSet)
			.Select(texture => texture.ToTexture());

		public override IEnumerable<Sound> Sounds => _tableComponent.LegacyContainer.Sounds
			.Where(sound => sound.IsSet)
			.Select(sound => sound.ToSound());

		private string[] TextureNames => _tableComponent.LegacyContainer.Textures
			.Select(t => t.Name)
			.ToArray();

		private string[] MaterialNames => _tableComponent.LegacyContainer.TableData.Materials
			.Select(m => m.Name)
			.ToArray();

		private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

		public override Material GetMaterial(string name)
		{
			if (string.IsNullOrEmpty(name)) {
				return null;
			}
			return _materials.ContainsKey(name.ToLower()) ? _materials[name.ToLower()] : null;
		}

		public override Texture GetTexture(string name) => null;

		private readonly TableComponent _tableComponent;
		private Table _table;

		public SceneTableContainer(TableComponent ta)
		{
			_tableComponent = ta;

		}

		public void Refresh(bool forExport = false)
		{
			var stopWatch = Stopwatch.StartNew();
			Clear();
			WalkChildren(_tableComponent.transform, node => RefreshChild(node, forExport));

			_tableComponent.CopyDataTo(_tableComponent.LegacyContainer.TableData, MaterialNames, TextureNames, forExport);
			var playfieldComponent = _tableComponent.GetComponentInChildren<PlayfieldComponent>();
			playfieldComponent.CopyDataTo(_tableComponent.LegacyContainer.TableData, MaterialNames, TextureNames, forExport);

			foreach (var material in _tableComponent.LegacyContainer.TableData.Materials) {
				_materials[material.Name.ToLower()] = material;
			}

			Logger.Info($"Refreshed {GameItems.Count()} game items and {_materials.Count} materials in {stopWatch.ElapsedMilliseconds}ms.");
		}

		public override void Save(string fileName)
		{
			Refresh(true);
			PrepareForExport();

			base.Save(fileName);
		}

		private void PrepareForExport()
		{
			// fetch legacy items from container (because they are not in the scene)
			foreach (var decal in _tableComponent.LegacyContainer.Decals) {
				_decals.Add(new Decal(decal));
			}
			foreach (var dispReel in _tableComponent.LegacyContainer.DispReels) {
				_dispReels[dispReel.Name] = new DispReel(dispReel);
			}
			foreach (var flasher in _tableComponent.LegacyContainer.Flashers) {
				_flashers[flasher.Name] = new Flasher(flasher);
			}
			foreach (var lightSeq in _tableComponent.LegacyContainer.LightSeqs) {
				_lightSeqs[lightSeq.Name] = new LightSeq(lightSeq);
			}
			foreach (var textBox in _tableComponent.LegacyContainer.TextBoxes) {
				_textBoxes[textBox.Name] = new TextBox(textBox);
			}
			foreach (var timer in _tableComponent.LegacyContainer.Timers) {
				_timers[timer.Name] = new Timer(timer);
			}

			// count stuff and update table data
			_tableComponent.LegacyContainer.TableData.NumCollections = Collections.Count;
			_tableComponent.LegacyContainer.TableData.NumFonts = 0;                     // todo handle fonts
			_tableComponent.LegacyContainer.TableData.NumGameItems = RecomputeGameItemStorageIDs(ItemDatas);
			_tableComponent.LegacyContainer.TableData.NumVpeGameItems = RecomputeGameItemStorageIDs(VpeItemDatas);
			_tableComponent.LegacyContainer.TableData.NumTextures = _tableComponent.LegacyContainer.Textures.Count(t => t.IsSet);
			_tableComponent.LegacyContainer.TableData.NumSounds = _tableComponent.LegacyContainer.Sounds.Count(t => t.IsSet);
			_tableComponent.LegacyContainer.TableData.NumMaterials = _tableComponent.LegacyContainer.TableData.Materials.Length;

			// add/merge physical materials from asset folder
			#if UNITY_EDITOR
			var guids = AssetDatabase.FindAssets("t:PhysicsMaterial", null);
			foreach (var guid in guids) {
				var assetPath = AssetDatabase.GUIDToAssetPath(guid);
				var matAsset = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(assetPath);
				var name = Path.GetFileNameWithoutExtension(assetPath);
				if (!_materials.ContainsKey(name.ToLower())) {
					continue;
				}
				var matTable = _materials[name.ToLower()];
				matTable.Elasticity = matAsset.Elasticity;
				matTable.ElasticityFalloff = matAsset.ElasticityFalloff;
				matTable.Friction = matAsset.Friction;
				matTable.ScatterAngle = matAsset.ScatterAngle;
			}
			_tableComponent.LegacyContainer.TableData.Materials = _materials.Values.ToArray();
			_tableComponent.LegacyContainer.TableData.NumMaterials = _materials.Count;
			#endif
		}

		private static int RecomputeGameItemStorageIDs(IEnumerable<ItemData> datas)
		{
			var itemDatas = datas.ToArray();
			var assignedItems = from d in itemDatas where d.StorageIndex > -1 orderby d.StorageIndex select d;
			var unassignedItems = from d in itemDatas where d.StorageIndex == -1 select d;
			var orderedItems = assignedItems.Concat(unassignedItems).ToArray();

			if (orderedItems.Length != itemDatas.Length) {
				throw new Exception($"Internal error, orderedItems.Length = {orderedItems.Length}, while itemDatas.Length = {itemDatas.Length}.");
			}

			for (var i = 0; i < orderedItems.Length; i++) {
				orderedItems[i].StorageIndex = i;
			}

			return orderedItems.Length;
		}

		private IEnumerable<Sound> RetrieveSounds()
		{
			return Array.Empty<Sound>();
		}

		protected override void Clear()
		{
			base.Clear();
			_materials.Clear();
		}

		private static void WalkChildren(IEnumerable node, Action<Transform> action)
		{
			foreach (Transform childTransform in node) {
				action(childTransform);
				WalkChildren(childTransform, action);
			}
		}

		private void RefreshChild(Component node, bool forExport)
		{
			Add(node.GetComponent<IItemMainComponent>(), forExport);
		}

		private void Add(IItemMainComponent comp, bool forExport)
		{
			if (comp == null) {
				return;
			}
			var name = comp.name;
			switch (comp) {
				case BumperComponent bumperComponent:
					var bumperData = bumperComponent.CopyDataTo(_tableComponent.LegacyContainer.Bumpers.ContainsKey(name) ? _tableComponent.LegacyContainer.Bumpers[name] : new BumperData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Bumper(bumperData));
					break;
				case FlipperComponent flipperComponent:
					var flipperData = flipperComponent.CopyDataTo(_tableComponent.LegacyContainer.Flippers.ContainsKey(name) ? _tableComponent.LegacyContainer.Flippers[name] : new FlipperData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Flipper(flipperData));
					break;
				case GateComponent gateComponent:
					var gatData = gateComponent.CopyDataTo(_tableComponent.LegacyContainer.Gates.ContainsKey(name) ? _tableComponent.LegacyContainer.Gates[name] : new GateData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Gate(gatData));
					break;
				case TargetComponent targetComponent:
					var hitTargetData = targetComponent.CopyDataTo(_tableComponent.LegacyContainer.HitTargets.ContainsKey(name) ? _tableComponent.LegacyContainer.HitTargets[name] : new HitTargetData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new HitTarget(hitTargetData));
					break;
				case KickerComponent kickerComponent:
					var kickerData = kickerComponent.CopyDataTo(_tableComponent.LegacyContainer.Kickers.ContainsKey(name) ? _tableComponent.LegacyContainer.Kickers[name] : new KickerData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Kicker(kickerData));
					break;
				case LightComponent lightComponent:
					var lightData = lightComponent.CopyDataTo(_tableComponent.LegacyContainer.Lights.ContainsKey(name) ? _tableComponent.LegacyContainer.Lights[name] : new LightData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Light(lightData));
					break;
				case PlungerComponent plungerComponent:
					var plungerData = plungerComponent.CopyDataTo(_tableComponent.LegacyContainer.Plungers.ContainsKey(name) ? _tableComponent.LegacyContainer.Plungers[name] : new PlungerData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Plunger(plungerData));
					break;
				case PrimitiveComponent primitiveComponent:
					var primitiveData = primitiveComponent.CopyDataTo(_tableComponent.LegacyContainer.Primitives.ContainsKey(name) ? _tableComponent.LegacyContainer.Primitives[name] : new PrimitiveData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Primitive(primitiveData));
					break;
				case RampComponent rampComponent:
					var rampData = rampComponent.CopyDataTo(_tableComponent.LegacyContainer.Ramps.ContainsKey(name) ? _tableComponent.LegacyContainer.Ramps[name] : new RampData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Ramp(rampData));
					break;
				case RubberComponent rubberComponent:
					var rubberData = rubberComponent.CopyDataTo(_tableComponent.LegacyContainer.Rubbers.ContainsKey(name) ? _tableComponent.LegacyContainer.Rubbers[name] : new RubberData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Rubber(rubberData));
					break;
				case SpinnerComponent spinnerComponent:
					var spinnerData = spinnerComponent.CopyDataTo(_tableComponent.LegacyContainer.Spinners.ContainsKey(name) ? _tableComponent.LegacyContainer.Spinners[name] : new SpinnerData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Spinner(spinnerData));
					break;
				case SurfaceComponent surfaceComponent:
					var surfaceData = surfaceComponent.CopyDataTo(_tableComponent.LegacyContainer.Surfaces.ContainsKey(name) ? _tableComponent.LegacyContainer.Surfaces[name] : new SurfaceData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Surface(surfaceData));
					break;
				case TriggerComponent triggerComponent:
					var triggerData = triggerComponent.CopyDataTo(_tableComponent.LegacyContainer.Triggers.ContainsKey(name) ? _tableComponent.LegacyContainer.Triggers[name] : new TriggerData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Trigger(triggerData));
					break;
				case TroughComponent troughComponent:
					var troughData = troughComponent.CopyDataTo(new TroughData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Trough(troughData));
					break;
			}
		}

		private void Add<T>(string name, T item) where T : IItem
		{
			var dict = GetItemDictionary<T>();
			if (dict.ContainsKey(name.ToLower())) {
				Logger.Warn($"{item.GetType()} {name} already added.");
			} else {
				dict.Add(name.ToLower(), item);
			}
		}
	}
}
