// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public enum DmdProjectTreeSelectionKind
	{
		Cue,
		Layer,
		Sprite,
		Font,
		Palette,
		SampleState,
	}

	public sealed class DmdProjectTreeSelection
	{
		public DmdProjectTreeSelectionKind Kind { get; }
		public UnityEngine.Object Asset { get; }
		public int LayerIndex { get; }
		public int SampleStateIndex { get; }

		internal DmdProjectTreeSelection(DmdProjectTreeSelectionKind kind, UnityEngine.Object asset,
			int layerIndex, int sampleStateIndex)
		{
			Kind = kind;
			Asset = asset;
			LayerIndex = layerIndex;
			SampleStateIndex = sampleStateIndex;
		}
	}

	public sealed class DmdProjectTreeView : TreeView
	{
		private int _nextId;
		private int _restoreId;
		private DmdProjectTreeSelection _restoreSelection;

		public event Action<DmdProjectTreeSelection> ItemSelected;

		public DmdProjectTreeView()
		{
			selectionType = SelectionType.Single;
			makeItem = () => new Label();
			bindItem = (element, index) => {
				var entry = GetItemDataForIndex<TreeEntry>(index);
				((Label)element).text = entry?.Label ?? string.Empty;
			};
			selectionChanged += selection => {
				var entry = selection?.OfType<TreeEntry>().FirstOrDefault();
				if (entry?.Selection != null) {
					ItemSelected?.Invoke(entry.Selection);
				}
			};
		}

		public void SetProject(DmdProjectAsset project, DmdProjectTreeSelection selection = null)
		{
			_nextId = 1;
			_restoreId = -1;
			_restoreSelection = selection;
			var roots = new List<TreeViewItemData<TreeEntry>> {
				Section("Cues", CueEntries(project?.Cues)),
				Section("Sprites", AssetEntries(project?.Sprites, DmdProjectTreeSelectionKind.Sprite)),
				Section("Fonts", AssetEntries(project?.Fonts, DmdProjectTreeSelectionKind.Font)),
				Section("Palettes", AssetEntries(project?.Palettes, DmdProjectTreeSelectionKind.Palette)),
				Section("Sample States", SampleStateEntries(project?.SampleStates))
			};
			SetRootItems(roots);
			Rebuild();
			ExpandAll();
			if (_restoreId >= 0) {
				SetSelectionByIdWithoutNotify(new[] { _restoreId });
			}
		}

		private TreeViewItemData<TreeEntry> Section(string label, List<TreeViewItemData<TreeEntry>> children)
		{
			return new TreeViewItemData<TreeEntry>(_nextId++, new TreeEntry(label), children);
		}

		private List<TreeViewItemData<TreeEntry>> CueEntries(IList<DmdCueAsset> cues)
		{
			var entries = new List<TreeViewItemData<TreeEntry>>();
			if (cues == null) {
				return entries;
			}
			foreach (var cue in cues) {
				if (cue == null) {
					continue;
				}
				var layers = new List<TreeViewItemData<TreeEntry>>();
				if (cue.Layers != null) {
					for (var layerIndex = 0; layerIndex < cue.Layers.Count; layerIndex++) {
						var layer = cue.Layers[layerIndex];
						var label = string.IsNullOrWhiteSpace(layer?.Name)
							? layer?.GetType().Name ?? "Missing Layer"
							: layer.Name;
						layers.Add(Item(label, DmdProjectTreeSelectionKind.Layer, cue, layerIndex));
					}
				}
				var selection = new DmdProjectTreeSelection(DmdProjectTreeSelectionKind.Cue, cue, -1, -1);
				var id = _nextId++;
				RememberSelection(id, selection);
				entries.Add(new TreeViewItemData<TreeEntry>(id, new TreeEntry(cue.EffectiveId, selection), layers));
			}
			return entries;
		}

		private List<TreeViewItemData<TreeEntry>> AssetEntries<T>(IList<T> assets,
			DmdProjectTreeSelectionKind kind) where T : UnityEngine.Object
		{
			var entries = new List<TreeViewItemData<TreeEntry>>();
			if (assets == null) {
				return entries;
			}
			foreach (var asset in assets) {
				if (asset != null) {
					entries.Add(Item(asset.name, kind, asset));
				}
			}
			return entries;
		}

		private List<TreeViewItemData<TreeEntry>> SampleStateEntries(IList<DmdSampleState> states)
		{
			var entries = new List<TreeViewItemData<TreeEntry>>();
			if (states == null) {
				return entries;
			}
			for (var index = 0; index < states.Count; index++) {
				entries.Add(Item(states[index]?.Name ?? $"State {index + 1}",
					DmdProjectTreeSelectionKind.SampleState, sampleStateIndex: index));
			}
			return entries;
		}

		private TreeViewItemData<TreeEntry> Item(string label, DmdProjectTreeSelectionKind kind,
			UnityEngine.Object asset = null, int layerIndex = -1, int sampleStateIndex = -1)
		{
			var selection = new DmdProjectTreeSelection(kind, asset, layerIndex, sampleStateIndex);
			var id = _nextId++;
			RememberSelection(id, selection);
			return new TreeViewItemData<TreeEntry>(id, new TreeEntry(label, selection));
		}

		private void RememberSelection(int id, DmdProjectTreeSelection selection)
		{
			if (_restoreSelection != null && selection.Kind == _restoreSelection.Kind &&
			    selection.Asset == _restoreSelection.Asset && selection.LayerIndex == _restoreSelection.LayerIndex &&
			    selection.SampleStateIndex == _restoreSelection.SampleStateIndex) {
				_restoreId = id;
			}
		}

		private sealed class TreeEntry
		{
			public string Label { get; }
			public DmdProjectTreeSelection Selection { get; }

			public TreeEntry(string label, DmdProjectTreeSelection selection = null)
			{
				Label = label;
				Selection = selection;
			}
		}
	}
}
