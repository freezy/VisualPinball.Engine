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

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using Material = VisualPinball.Engine.VPT.Material;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity
{
	public class SceneTableContainer : TableContainer
	{
		public override Table Table => _tableAuthoring.Table;
		public override Dictionary<string, string> TableInfo => _tableAuthoring.TableInfo;
		public override List<CollectionData> Collections => _tableAuthoring.Collections;
		public override Mappings Mappings => new Mappings(_tableAuthoring.Mappings);
		public override CustomInfoTags CustomInfoTags => _tableAuthoring.CustomInfoTags;

		public const int ChildObjectsLayer = 16;

		private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

		public override Material GetMaterial(string name)
		{
			if (string.IsNullOrEmpty(name)) {
				return null;
			}
			return _materials.ContainsKey(name.ToLower()) ? _materials[name.ToLower()] : null;
		}

		public override Texture GetTexture(string name)
		{
			return null;
		}

		private readonly TableAuthoring _tableAuthoring;

		public SceneTableContainer(TableAuthoring ta)
		{
			_tableAuthoring = ta;
		}

		public void Refresh()
		{
			var stopWatch = Stopwatch.StartNew();
			Clear();
			WalkChildren(_tableAuthoring.transform);

			foreach (var material in _tableAuthoring.Data.Materials) {
				_materials[material.Name.ToLower()] = material;
			}

			Logger.Info($"Refreshed {GameItems.Count()} game items in {stopWatch.ElapsedMilliseconds}ms.");
		}

		public void PrepareForExport()
		{
			// refresh first
			Refresh();

			// fetch legacy items from container (because they are not in the scene)
			foreach (var decal in _tableAuthoring.LegacyContainer.decals) {
				_decals.Add(new Decal(decal));
			}
			foreach (var dispReel in _tableAuthoring.LegacyContainer.dispReels) {
				_dispReels[dispReel.Name] = new DispReel(dispReel);
			}
			foreach (var flasher in _tableAuthoring.LegacyContainer.flashers) {
				_flashers[flasher.Name] = new Flasher(flasher);
			}
			foreach (var lightSeq in _tableAuthoring.LegacyContainer.lightSeqs) {
				_lightSeqs[lightSeq.Name] = new LightSeq(lightSeq);
			}
			foreach (var textBox in _tableAuthoring.LegacyContainer.textBoxes) {
				_textBoxes[textBox.Name] = new TextBox(textBox);
			}
			foreach (var timer in _tableAuthoring.LegacyContainer.timers) {
				_timers[timer.Name] = new Timer(timer);
			}

			// count stuff and update table data counters
			Table.Data.NumCollections = Collections.Count;
			Table.Data.NumFonts = 0; // todo handle fonts?
			Table.Data.NumGameItems = ItemDatas.Count();

			// todo both!
			Table.Data.NumSounds = 0;
			Table.Data.NumTextures = 0;

			// add/merge physical materials from asset folder
			#if UNITY_EDITOR
			var guids = UnityEditor.AssetDatabase.FindAssets("t:PhysicsMaterial", null);
			foreach (var guid in guids) {
				var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
				var matAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(assetPath);
				var name = Path.GetFileNameWithoutExtension(assetPath);
				if (!_materials.ContainsKey(name.ToLower())) {
					_materials[name.ToLower()] = new Material();
				}
				var matTable = _materials[name.ToLower()];
				matTable.Elasticity = matAsset.Elasticity;
				matTable.ElasticityFalloff = matAsset.ElasticityFalloff;
				matTable.Friction = matAsset.Friction;
				matTable.ScatterAngle = matAsset.ScatterAngle;
			}
			#endif
			Table.Data.NumMaterials = _materials.Count;
		}

		protected override void Clear()
		{
			base.Clear();
			_materials.Clear();
		}

		private void WalkChildren(IEnumerable node)
		{
			foreach (Transform childTransform in node) {
				RefreshChild(childTransform);
				WalkChildren(childTransform);
			}
		}

		private void RefreshChild(Component node)
		{
			Add(node.GetComponent<IItemMainAuthoring>());
		}

		private void Add(IItemMainAuthoring comp)
		{
			if (comp == null) {
				return;
			}
			switch (comp) {
				case BumperAuthoring bumperAuthoring:
					Add(comp.gameObject.name, bumperAuthoring.Item);
					break;
				case FlipperAuthoring flipperAuthoring:
					Add(comp.gameObject.name, flipperAuthoring.Item);
					break;
				case GateAuthoring gateAuthoring:
					Add(comp.gameObject.name, gateAuthoring.Item);
					break;
				case HitTargetAuthoring hitTargetAuthoring:
					Add(comp.gameObject.name, hitTargetAuthoring.Item);
					break;
				case KickerAuthoring kickerAuthoring:
					Add(comp.gameObject.name, kickerAuthoring.Item);
					break;
				case LightAuthoring lightAuthoring:
					Add(comp.gameObject.name, lightAuthoring.Item);
					break;
				case PlungerAuthoring plungerAuthoring:
					Add(comp.gameObject.name, plungerAuthoring.Item);
					break;
				case PrimitiveAuthoring primitiveAuthoring:
					Add(comp.gameObject.name, primitiveAuthoring.Item);
					break;
				case RampAuthoring rampAuthoring:
					Add(comp.gameObject.name, rampAuthoring.Item);
					break;
				case RubberAuthoring rubberAuthoring:
					Add(comp.gameObject.name, rubberAuthoring.Item);
					break;
				case SpinnerAuthoring spinnerAuthoring:
					Add(comp.gameObject.name, spinnerAuthoring.Item);
					break;
				case SurfaceAuthoring surfaceAuthoring:
					Add(comp.gameObject.name, surfaceAuthoring.Item);
					break;
				case TriggerAuthoring triggerAuthoring:
					Add(comp.gameObject.name, triggerAuthoring.Item);
					break;
				case TroughAuthoring troughAuthoring:
					Add(comp.gameObject.name, troughAuthoring.Item);
					break;
			}
		}

		private void Add<T>(string name, T item) where T : IItem
		{
			var dict = GetItemDictionary<T>();
			if (dict.ContainsKey(name)) {
				Logger.Warn($"{item.GetType()} {name} already added.");
			} else {
				dict.Add(name, item);
			}
		}
	}
}
