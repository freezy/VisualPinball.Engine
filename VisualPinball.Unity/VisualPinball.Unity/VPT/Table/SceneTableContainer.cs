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
		public override Table Table => _table ??= new Table(_tableAuthoring.TableContainer, _tableAuthoring.LegacyContainer.TableData);
		public override Dictionary<string, string> TableInfo => _tableAuthoring.TableInfo;
		public override List<CollectionData> Collections => _tableAuthoring.Collections;
		[Obsolete("Use MappingConfig")]
		public override CustomInfoTags CustomInfoTags => _tableAuthoring.CustomInfoTags;

		public const int ChildObjectsLayer = 16;

		public override IEnumerable<Texture> Textures => _tableAuthoring.LegacyContainer.Textures
			.Where(texture => texture.IsSet)
			.Select(texture => texture.ToTexture());

		public override IEnumerable<Sound> Sounds => _tableAuthoring.LegacyContainer.Sounds
			.Where(sound => sound.IsSet)
			.Select(sound => sound.ToSound());

		private string[] TextureNames => _tableAuthoring.LegacyContainer.Textures
			.Select(t => t.Name)
			.ToArray();

		private string[] MaterialNames => _tableAuthoring.LegacyContainer.TableData.Materials
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

		private readonly TableAuthoring _tableAuthoring;
		private Table _table;

		public SceneTableContainer(TableAuthoring ta)
		{
			_tableAuthoring = ta;

		}

		public void Refresh(bool forExport = false)
		{
			var stopWatch = Stopwatch.StartNew();
			Clear();
			WalkChildren(_tableAuthoring.transform, node => RefreshChild(node, forExport));

			_tableAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.TableData, MaterialNames, TextureNames, forExport);
			var playfieldAuthoring = _tableAuthoring.GetComponentInChildren<PlayfieldAuthoring>();
			playfieldAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.TableData, MaterialNames, TextureNames, forExport);

			foreach (var material in _tableAuthoring.LegacyContainer.TableData.Materials) {
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
			foreach (var decal in _tableAuthoring.LegacyContainer.Decals) {
				_decals.Add(new Decal(decal));
			}
			foreach (var dispReel in _tableAuthoring.LegacyContainer.DispReels) {
				_dispReels[dispReel.Name] = new DispReel(dispReel);
			}
			foreach (var flasher in _tableAuthoring.LegacyContainer.Flashers) {
				_flashers[flasher.Name] = new Flasher(flasher);
			}
			foreach (var lightSeq in _tableAuthoring.LegacyContainer.LightSeqs) {
				_lightSeqs[lightSeq.Name] = new LightSeq(lightSeq);
			}
			foreach (var textBox in _tableAuthoring.LegacyContainer.TextBoxes) {
				_textBoxes[textBox.Name] = new TextBox(textBox);
			}
			foreach (var timer in _tableAuthoring.LegacyContainer.Timers) {
				_timers[timer.Name] = new Timer(timer);
			}

			// count stuff and update table data
			_tableAuthoring.LegacyContainer.TableData.NumCollections = Collections.Count;
			_tableAuthoring.LegacyContainer.TableData.NumFonts = 0;                     // todo handle fonts
			_tableAuthoring.LegacyContainer.TableData.NumGameItems = RecomputeGameItemStorageIDs(ItemDatas);
			_tableAuthoring.LegacyContainer.TableData.NumVpeGameItems = RecomputeGameItemStorageIDs(VpeItemDatas);
			_tableAuthoring.LegacyContainer.TableData.NumTextures = _tableAuthoring.LegacyContainer.Textures.Count(t => t.IsSet);
			_tableAuthoring.LegacyContainer.TableData.NumSounds = _tableAuthoring.LegacyContainer.Sounds.Count(t => t.IsSet);
			_tableAuthoring.LegacyContainer.TableData.NumMaterials = _tableAuthoring.LegacyContainer.TableData.Materials.Length;

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
			_tableAuthoring.LegacyContainer.TableData.Materials = _materials.Values.ToArray();
			_tableAuthoring.LegacyContainer.TableData.NumMaterials = _materials.Count;
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
			Add(node.GetComponent<IItemMainAuthoring>(), forExport);
		}

		private void Add(IItemMainAuthoring comp, bool forExport)
		{
			if (comp == null) {
				return;
			}
			var name = comp.name;
			switch (comp) {
				case BumperAuthoring bumperAuthoring:
					var bumperData = bumperAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Bumpers.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Bumpers[name] : new BumperData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Bumper(bumperData));
					break;
				case FlipperAuthoring flipperAuthoring:
					var flipperData = flipperAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Flippers.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Flippers[name] : new FlipperData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Flipper(flipperData));
					break;
				case GateAuthoring gateAuthoring:
					var gatData = gateAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Gates.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Gates[name] : new GateData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Gate(gatData));
					break;
				case TargetAuthoring hitTargetAuthoring:
					var hitTargetData = hitTargetAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.HitTargets.ContainsKey(name) ? _tableAuthoring.LegacyContainer.HitTargets[name] : new HitTargetData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new HitTarget(hitTargetData));
					break;
				case KickerAuthoring kickerAuthoring:
					var kickerData = kickerAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Kickers.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Kickers[name] : new KickerData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Kicker(kickerData));
					break;
				case LightAuthoring lightAuthoring:
					var lightData = lightAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Lights.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Lights[name] : new LightData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Light(lightData));
					break;
				case PlungerAuthoring plungerAuthoring:
					var plungerData = plungerAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Plungers.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Plungers[name] : new PlungerData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Plunger(plungerData));
					break;
				case PrimitiveAuthoring primitiveAuthoring:
					var primitiveData = primitiveAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Primitives.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Primitives[name] : new PrimitiveData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Primitive(primitiveData));
					break;
				case RampAuthoring rampAuthoring:
					var rampData = rampAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Ramps.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Ramps[name] : new RampData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Ramp(rampData));
					break;
				case RubberAuthoring rubberAuthoring:
					var rubberData = rubberAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Rubbers.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Rubbers[name] : new RubberData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Rubber(rubberData));
					break;
				case SpinnerAuthoring spinnerAuthoring:
					var spinnerData = spinnerAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Spinners.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Spinners[name] : new SpinnerData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Spinner(spinnerData));
					break;
				case SurfaceAuthoring surfaceAuthoring:
					var surfaceData = surfaceAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Surfaces.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Surfaces[name] : new SurfaceData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Surface(surfaceData));
					break;
				case TriggerAuthoring triggerAuthoring:
					var triggerData = triggerAuthoring.CopyDataTo(_tableAuthoring.LegacyContainer.Triggers.ContainsKey(name) ? _tableAuthoring.LegacyContainer.Triggers[name] : new TriggerData(), MaterialNames, TextureNames, forExport);
					Add(comp.gameObject.name, new Trigger(triggerData));
					break;
				case TroughAuthoring troughAuthoring:
					var troughData = troughAuthoring.CopyDataTo(new TroughData(), MaterialNames, TextureNames, forExport);
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
