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
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using VisualPinball.Unity.Playfield;
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

		public override IEnumerable<Texture> Textures => _tableAuthoring.LegacyContainer.Textures
			.Where(texture => texture.IsSet)
			.Select(texture => texture.ToTexture());
		public override IEnumerable<Sound> Sounds => _tableAuthoring.LegacyContainer.Sounds
			.Where(sound => sound.IsSet)
			.Select(sound => sound.ToSound());

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
			WalkChildren(_tableAuthoring.transform, RefreshChild);

			foreach (var material in _tableAuthoring.Data.Materials) {
				_materials[material.Name.ToLower()] = material;
			}

			Logger.Info($"Refreshed {GameItems.Count()} game items in {stopWatch.ElapsedMilliseconds}ms.");
		}

		public override void Save(string fileName)
		{
			Refresh();
			FillBinaryData();
			PrepareForExport();

			base.Save(fileName);

			FreeBinaryData();
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
			Table.Data.NumCollections = Collections.Count;
			Table.Data.NumFonts = 0;                     // todo handle fonts
			Table.Data.NumGameItems = RecomputeGameItemStorageIDs();
			Table.Data.NumTextures = _tableAuthoring.LegacyContainer.Textures.Count(t => t.IsSet);
			Table.Data.NumSounds = _tableAuthoring.LegacyContainer.Sounds.Count(t => t.IsSet);

			// update texture references
			WalkChildren(_tableAuthoring.transform, SetTextureReference);

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

		private static void SetTextureReference(Component node)
		{
			var mr = node.GetComponent<MeshRenderer>();
			if (!mr) {
				return;
			}
			var meshAuthoring = node.GetComponent<IItemMeshAuthoring>();
			if (meshAuthoring == null) {
				return;
			}
			switch (meshAuthoring) {
				case FlipperBaseMeshAuthoring flipperBase: {
					flipperBase.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
				case HitTargetMeshAuthoring hitTarget: {
					hitTarget.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
				case PlungerMeshAuthoring plunger: {
					plunger.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
				case PrimitiveMeshAuthoring primitive: {
					primitive.MainAuthoring.Data.Image = GetTextureName(mr);
					primitive.MainAuthoring.Data.NormalMap = GetNormalTextureName(mr);
					break;
				}
				case RampFloorMeshAuthoring rampFloor: {
					rampFloor.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
				case RampWallMeshAuthoring rampWall: {
					rampWall.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
				case RubberMeshAuthoring rubber: {
					rubber.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
				case SpinnerBracketMeshAuthoring spinnerBracket: {
					spinnerBracket.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
				case SpinnerPlateMeshAuthoring spinnerPlate: {
					spinnerPlate.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
				case SurfaceSideMeshAuthoring surfaceSide: {
					surfaceSide.MainAuthoring.Data.SideImage = GetTextureName(mr);
					break;
				}
				case SurfaceTopMeshAuthoring surfaceTop: {
					surfaceTop.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
				case PlayfieldMeshAuthoring playfield: {
					playfield.MainAuthoring.Data.Image = GetTextureName(mr);
					break;
				}
			}
		}

		private static string GetTextureName(Renderer mr)
		{
			if (mr == null || mr.sharedMaterial == null || mr.sharedMaterial.mainTexture == null) {
				return string.Empty;
			}
			return mr.sharedMaterial.mainTexture.name;
		}

		private static string GetNormalTextureName(Renderer mr)
		{
			if (mr == null || mr.sharedMaterial == null) {
				return string.Empty;
			}
			var tex = mr.sharedMaterial.GetTexture(RenderPipeline.Current.MaterialConverter.NormalMapProperty);
			return tex == null ? string.Empty : tex.name;
		}

		private int RecomputeGameItemStorageIDs()
		{
			#if !WRITE_VP106 && !WRITE_VP107
				var itemDatas = ItemDatas.ToArray();
			#else
				var itemDatas = ItemDatas.Where(d => d.IsVpCompatible).ToArray();
			#endif
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

		private void FillBinaryData()
		{
			WalkChildren(_tableAuthoring.transform, FillBinaryData);
		}

		private void FreeBinaryData()
		{
			WalkChildren(_tableAuthoring.transform, FreeBinaryData);
		}

		private static void FillBinaryData(Transform transform)
		{
			var comp = transform.GetComponent<IItemMainAuthoring>();
			comp?.FillBinaryData();
		}

		private static void FreeBinaryData(Transform transform)
		{
			var comp = transform.GetComponent<IItemMainAuthoring>();
			comp?.FreeBinaryData();
		}

		private IEnumerable<Sound> RetrieveSounds()
		{
			return new Sound[0];
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
