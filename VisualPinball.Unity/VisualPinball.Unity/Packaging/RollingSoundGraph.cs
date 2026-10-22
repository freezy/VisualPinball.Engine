// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisualPinball.Unity
{
	/// <summary>Optional, package-local acoustic graph. Kept outside generic assets so older readers can skip it.</summary>
	public sealed class RollingSoundGraph
	{
		public const int Version = 1;
		private readonly PackagedFiles _files;
		private readonly IPackageFolder _folder;
		private readonly Dictionary<BallSoundMaterialAsset, int> _ballIds = new();
		private readonly Dictionary<RollingSoundMaterialAsset, int> _surfaceIds = new();
		private readonly Dictionary<int, BallSoundMaterialAsset> _balls = new();
		private readonly Dictionary<int, RollingSoundMaterialAsset> _surfaces = new();
		private readonly List<ScriptableObject> _owned = new();
		private bool _supported = true;
		internal RollingSoundGraph(PackagedFiles files, IPackageFolder folder) { _files = files; _folder = folder; }
		public int AddBall(BallSoundMaterialAsset asset)
		{
			if (!asset) return 0;
			if (!_ballIds.TryGetValue(asset, out var id)) _ballIds.Add(asset, id = _ballIds.Count + 1);
			return id;
		}
		public int AddSurface(RollingSoundMaterialAsset asset)
		{
			if (!asset) return 0;
			if (_surfaceIds.TryGetValue(asset, out var id)) return id;
			_surfaceIds.Add(asset, id = _surfaceIds.Count + 1);
			AddBall(asset.FallbackBallMaterial);
			foreach (var row in asset.Responses ?? Array.Empty<RollingSoundResponse>()) {
				if (row == null) continue;
				AddBall(row.BallMaterial); _files.AddAsset<SoundEffectAsset>(row.Loop); _files.AddAsset<SoundEffectAsset>(row.Enter); _files.AddAsset<SoundEffectAsset>(row.Exit);
			}
			return id;
		}
		public BallSoundMaterialAsset Ball(int id) => id == 0 || !_supported ? null : _balls.TryGetValue(id, out var asset) ? asset : throw new InvalidDataException("Missing rolling ball material " + id);
		public RollingSoundMaterialAsset Surface(int id) => id == 0 || !_supported ? null : _surfaces.TryGetValue(id, out var asset) ? asset : throw new InvalidDataException("Missing rolling surface material " + id);

		internal void Write(IEnumerable<PhysicsMaterialAsset> physicalMaterials)
		{
			var bindings = physicalMaterials.Where(material => material.RollingSoundMaterial).Select(material => new Binding { Physical = _files.AddAsset<PhysicsMaterialAsset>(material), Surface = AddSurface(material.RollingSoundMaterial) }).ToArray();
			if (_surfaceIds.Count == 0 && _ballIds.Count == 0) return;
			var graph = new Graph { Schema = Version, Bindings = bindings,
				Balls = _ballIds.Select(pair => new BallData { Id = pair.Value, Name = pair.Key.name }).ToArray(),
				Surfaces = _surfaceIds.Select(pair => new SurfaceData { Id = pair.Value, Name = pair.Key.name, Group = pair.Key.Group, VariantMode = pair.Key.VariantMode, SlotPool = pair.Key.SlotPool, SlotCount = pair.Key.SlotCount, Fallback = AddBall(pair.Key.FallbackBallMaterial),
					Rows = pair.Key.Responses.Where(row => row != null).Select(row => new Row { Settings = row, Ball = AddBall(row.BallMaterial), Loop = _files.AddAsset<SoundEffectAsset>(row.Loop), Enter = _files.AddAsset<SoundEffectAsset>(row.Enter), Exit = _files.AddAsset<SoundEffectAsset>(row.Exit) }).ToArray() }).ToArray()
			};
			_folder.AddFolder("rolling").AddFile("graph", PackageApi.Packer.FileExtension).SetData(PackageApi.Packer.Pack(graph));
		}

		internal void Read(string editorAssetPath = null, int schema = Version)
		{
			if (!_folder.TryGetFolder("rolling", out var folder) || !folder.TryGetFile("graph", out var file, PackageApi.Packer.FileExtension)) return;
			if (schema != Version) { _supported = false; Debug.LogWarning("Unsupported rolling audio schema; rolling audio is disabled for this table."); return; }
			var graph = PackageApi.Packer.Unpack<Graph>(file.GetData());
			if (graph.Schema != Version) { _supported = false; return; }
			try {
				// Allocate every identity before resolving any edge. Zip entry order never affects references.
				foreach (var data in graph.Balls) { var ball = ScriptableObject.CreateInstance<BallSoundMaterialAsset>(); ball.name = data.Name; _owned.Add(ball); _balls.Add(data.Id, ball); }
				foreach (var data in graph.Surfaces) { var surface = ScriptableObject.CreateInstance<RollingSoundMaterialAsset>(); surface.name = data.Name; _owned.Add(surface); _surfaces.Add(data.Id, surface); }
				foreach (var data in graph.Surfaces) {
					var surface = Surface(data.Id); surface.Group = data.Group; surface.VariantMode = data.VariantMode; surface.SlotPool = data.SlotPool; surface.SlotCount = data.SlotCount; surface.FallbackBallMaterial = Ball(data.Fallback);
					surface.Responses = data.Rows.Select(row => { var settings = row.Settings; settings.BallMaterial = Ball(row.Ball); settings.Loop = Bank(row.Loop); settings.Enter = Bank(row.Enter); settings.Exit = Bank(row.Exit); return settings; }).ToArray();
				}
				foreach (var binding in graph.Bindings) {
					var physical = _files.GetAsset<PhysicsMaterialAsset>(binding.Physical);
					if (!physical) throw new InvalidDataException("Missing physics material for rolling binding " + binding.Physical);
					physical.RollingSoundMaterial = Surface(binding.Surface);
#if UNITY_EDITOR
					if (editorAssetPath != null) EditorUtility.SetDirty(physical);
#endif
				}
#if UNITY_EDITOR
				if (editorAssetPath != null) {
					var folderPath = Path.Combine(editorAssetPath, "Rolling"); Directory.CreateDirectory(folderPath);
					var relative = Path.GetRelativePath(Path.Combine(Application.dataPath, ".."), folderPath);
					foreach (var asset in _owned) {
						var name = asset.name;
						var filename = string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
						if (string.IsNullOrWhiteSpace(filename)) filename = "Rolling Material";
						AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(Path.Combine(relative, filename + ".asset")));
						asset.name = name; EditorUtility.SetDirty(asset);
					}
					foreach (var asset in _owned) AssetDatabase.SaveAssetIfDirty(asset);
					foreach (var binding in graph.Bindings) AssetDatabase.SaveAssetIfDirty(_files.GetAsset<PhysicsMaterialAsset>(binding.Physical));
					_owned.Clear();
				}
#endif
			} catch { Dispose(); throw; }
		}

		private SoundEffectAsset Bank(int id) => id == 0 ? null : _files.GetAsset<SoundEffectAsset>(id) ?? throw new InvalidDataException("Missing rolling sound bank " + id);
		internal void AttachOwner(GameObject table)
		{
			if (!_supported) foreach (var component in table.GetComponentsInChildren<RollingSoundComponent>(true)) component.enabled = false;
			if (_owned.Count == 0) return;
			var owner = table.AddComponent<RollingSoundGraphOwner>(); owner.Assets.AddRange(_owned); _owned.Clear();
		}
		internal void Dispose() { foreach (var asset in _owned) { if (Application.isPlaying) UnityEngine.Object.Destroy(asset); else UnityEngine.Object.DestroyImmediate(asset); } _owned.Clear(); }

		[Serializable] private sealed class Graph { public int Schema; public BallData[] Balls; public SurfaceData[] Surfaces; public Binding[] Bindings; }
		[Serializable] private sealed class BallData { public int Id; public string Name; }
		[Serializable] private sealed class SurfaceData { public int Id; public string Name; public RollingSoundGroup Group; public RollingVariantMode VariantMode; public string SlotPool; public int SlotCount, Fallback; public Row[] Rows; }
		[Serializable] private sealed class Row { public int Ball, Loop, Enter, Exit; public RollingSoundResponse Settings; }
		[Serializable] private sealed class Binding { public int Physical, Surface; }
	}

	internal sealed class RollingSoundGraphOwner : MonoBehaviour
	{
		internal readonly List<ScriptableObject> Assets = new();
		private void OnDestroy() { foreach (var asset in Assets) { if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset); } Assets.Clear(); }
	}
}
