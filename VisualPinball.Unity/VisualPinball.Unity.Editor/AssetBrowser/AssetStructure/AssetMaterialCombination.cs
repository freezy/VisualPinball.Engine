// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using System.IO;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A material combination is an asset that gets one or more of its material slots overriden
	/// by a <see cref="AssetMaterialOverride"/> (which is linked to a <see cref="AssetMaterialVariation"/>).
	///
	/// Among those overrides, the AssetMaterialVariations are unique, i.e. a material slot can only be
	/// overridden once. The overrides aren't always complete, i.e. some material slots may not be overridden.
	/// </summary>
	public class AssetMaterialCombination
	{
		public readonly Asset Asset;
		private readonly VariationOverride[] _variations;

		private string _thumbId;

		public string Name => string.Join(", ",
			(_variations.Any(v => v.IsDecal)
				? _variations.Where(v => v.IsDecal)
				: _variations
			).Select(v => $"{v.Override.Name} {v.Variation.Name}")
		);

		public string MaterialName => string.Join(", ",
			_variations.Where(v => !v.IsDecal).Select(v => $"{v.Override.Name} {v.Variation.Name}")
		);

		public string ThumbId => GenerateThumbID();
		public string ThumbPath => $"{Asset.Library.ThumbnailRoot}/{ThumbId}.webp";

		public bool IsOriginal => _variations.Length == 0;

		public bool HasThumbnail => File.Exists(ThumbPath);

		public bool IsValidCombination {
			get {
				if (Asset.MaterialCombinationRules == null || Asset.MaterialCombinationRules.Count == 0) {
					return true;
				}

				var varsWithDefaults = VariationsWithDefaults;
				foreach (var rule in Asset.MaterialCombinationRules) {
					switch (rule.Type) {
						case AssetMaterialCombinationType.MustAllBeEqual: {
							var allEqual = varsWithDefaults
								.Where(v => rule.Targets.Any(t => t == v.Target))
								.GroupBy(v => v.Name)
								.Count() == 1;
							if (!allEqual) {
								return false;
							}
							break;
						}

						case AssetMaterialCombinationType.MustAllBeDifferent: {
							var allDifferent = varsWithDefaults
								.Where(v => rule.Targets.Any(t => t == v.Target))
								.GroupBy(v => v.Name)
								.Count() == varsWithDefaults.Length;
							if (!allDifferent) {
								return false;
							}
							break;
						}
					}
				}
				return true;
			}
		}

		public AssetMaterialCombination GetValidCombination()
		{
			if (IsValidCombination) {
				return this;
			}

			return GetCombinations(Asset, true)
				.Where(c => c.Contains(this))
				.First(c => c.IsValidCombination);
		}

		public bool Contains(AssetMaterialVariation variation)
		{
			if (variation == null || variation.Overrides.Count == 0) {
				return false;
			}

			return _variations.Any(v => v.Variation.GUID == variation.GUID);
		}

		private bool Contains(AssetMaterialCombination other)
		{
			if (other == null || other._variations.Length == 0) {
				return false;
			}
			return other._variations.All(otherVar => _variations.Contains(otherVar));
		}

		private VariationWithDefault[] VariationsWithDefaults =>
			_variations
				.Select(v => new VariationWithDefault(v.Variation.Target, v.Override.Name))
				.Concat(Asset.MaterialDefaults
					.Where(md => _variations.All(v => v.Variation.Target != md.Target))
					.Select(md => new VariationWithDefault(md.Target, md.Name)))
				.ToArray();

		public AssetMaterialCombination(Asset asset)
		{
			Asset = asset;
			_variations = Array.Empty<VariationOverride>();
		}

		public AssetMaterialCombination(Asset asset, AssetMaterialVariation variation, AssetMaterialOverride @override)
		{
			Asset = asset;
			_variations = new[] { new VariationOverride(variation, @override) };
		}

		public AssetMaterialCombination(Asset asset, VariationOverride[] variations)
		{
			Asset = asset;
			_variations = variations;
		}

		/// <summary>
		/// Maps each override of the given decal variation to a new material combination that consists of the
		/// current variations plus the override of the decal variation.
		/// </summary>
		/// <param name="decalVariation"></param>
		/// <returns></returns>
		public IEnumerable<AssetMaterialCombination> AddOverridesFrom(AssetMaterialVariation decalVariation) =>
			decalVariation.Overrides.Select(@override => WithOverride(decalVariation, @override));

		/// <summary>
		/// Creates a new material combination that consists of the current variations plus the given overrides
		/// </summary>
		/// <param name="variation"></param>
		/// <param name="override"></param>
		/// <returns></returns>
		private AssetMaterialCombination WithOverride(AssetMaterialVariation variation, AssetMaterialOverride @override)
		{
			return new AssetMaterialCombination(Asset, _variations
				//.Where(v => v.Variation.Slot != variation.Slot && v.Variation.Object != variation.Object)
				.Concat(new [] {new VariationOverride(variation, @override)})
				.ToArray()
			);
		}

		/// <summary>
		/// So this is basically a counter where the positions are the variations, and the figures are the overrides.
		/// When the last override of the last variation has counted up, we're done.
		/// </summary>
		/// <param name="asset"></param>
		/// <param name="includeDecals"></param>
		/// <returns></returns>
		public static IEnumerable<AssetMaterialCombination> GetCombinations(Asset asset, bool includeDecals = false)
		{
			var variations = new List<AssetMaterialVariation>();
			foreach (var childAsset in asset.GetNestedAssets()) {
				variations.AddRange(childAsset.MaterialVariations.Select(mv => mv.AsNested()));
			}

			variations.AddRange(asset.MaterialVariations);

			if (includeDecals && asset.DecalVariations.Count > 0) {
				variations.AddRange(asset.DecalVariations
					.GroupBy(dv => dv.Target)
					.Where(objectSlot => objectSlot?.Key != null)
					.Select(objectSlot => new AssetMaterialVariation {
						Name = objectSlot.Key.Object?.name ?? "<object unset>",
						Target = objectSlot.Key,
						Overrides = objectSlot.SelectMany(dv => dv.Overrides).ToList()
					}.AsDecal())
				);
			}

			var counters = new Counter[variations.Count];
			Counter nextCounter = null;
			for (var i = variations.Count - 1; i >= 0; i--) {
				counters[i] = new Counter(variations[i].Overrides.Count, nextCounter);
				nextCounter = counters[i];
			}

			var combinations = new List<AssetMaterialCombination>();
			if (counters.Length == 0) {
				combinations.Add(new AssetMaterialCombination(asset));
				return combinations;
			}
			do {
				combinations.Add(new AssetMaterialCombination(asset, counters, variations));
			} while (counters[0].Increase());

			return combinations;
		}


		public void MoveThumb(AssetLibrary destLibrary)
		{
			if (File.Exists(ThumbPath)) {
				var destPath = $"{destLibrary.ThumbnailRoot}/{ThumbId}.webp";
				File.Move(ThumbPath, destPath);
			}
		}

		public void ApplyObjectPos(GameObject go)
		{
			if (Asset.ThumbCameraPos == default) {
				Asset.ThumbCameraPos = new Vector3(0, Asset.ThumbCameraHeight, 0);
			}
			go.transform.position = Asset.ThumbCameraPos;
			go.transform.rotation = Quaternion.Euler(Asset.ThumbCameraRot);
		}

		public void ApplyMaterial(GameObject go)
		{
			foreach (var v in _variations) {
				var obj = v.Variation.Match(go);
				if (obj == null) {
					Debug.LogError("Unable to determine which to object the material needs to be applied to.");
					return;
				}

				if (!obj.activeSelf) {
					obj.SetActive(true);
				}
				var materials = obj.gameObject.GetComponent<MeshRenderer>().sharedMaterials;
				materials[v.Variation.Target.Slot] = v.Override.Material;
				obj.gameObject.GetComponent<MeshRenderer>().sharedMaterials = materials;
			}
		}

		/// <summary>
		/// We just munch all the override guids together on top of the asset guid.
		/// </summary>
		/// <returns></returns>
		private string GenerateThumbID()
		{
			if (_thumbId != null) {
				return _thumbId;
			}

			if (_variations.Length == 0) {
				_thumbId = Asset.GUID;

			} else {
				const int byteCount = 16;
				var guid1 = new Guid(Asset.GUID);
				foreach (var v in _variations) {
					var guid2 = new Guid(v.Override.Id);
					var destByte = new byte[byteCount];
					var guid1Byte = guid1.ToByteArray();
					var guid2Byte = guid2.ToByteArray();
					for (var i = 0; i < byteCount; i++) {
						destByte[i] = (byte) (guid1Byte[i] ^ guid2Byte[i]);
					}
					guid1 = new Guid(destByte);
				}
				_thumbId = guid1.ToString();
			}
			return _thumbId;
		}

		internal AssetMaterialCombination(Asset asset, IReadOnlyList<Counter> counters, IReadOnlyList<AssetMaterialVariation> variations)
		{
			Asset = asset;
			_variations = new VariationOverride[counters.Count];
			for (var i = 0; i < counters.Count; i++) {
				var overrideIndex = counters[i].Value;
				_variations[i] = new VariationOverride(
					overrideIndex == 0 ? null : variations[i],
					overrideIndex == 0 ? null : variations[i].Overrides[overrideIndex - 1]
				);
			}
			_variations = _variations.Where(mv => mv.Variation != null).ToArray();
		}

		internal class Counter
		{
			public int Value;
			private readonly int _size;
			private readonly Counter _nextCounter;

			public Counter(int size, Counter nextCounter)
			{
				_size = size;
				_nextCounter = nextCounter;
			}

			public bool Increase()
			{
				if (Value == _size) {
					if (_nextCounter != null) {
						Value = 0;
						return _nextCounter.Increase();
					}
					return false;
				}
				Value++;
				return true;
			}
		}

		public override string ToString() => $"{string.Join(" | ", _variations.Select(v => $"{v.Override.Name} {v.Variation.Name} ({v.Override.Material.name})"))}";

		public readonly struct VariationOverride : IEquatable<VariationOverride>
		{
			public readonly AssetMaterialVariation Variation;
			public readonly AssetMaterialOverride Override;
			public bool IsDecal => Variation is { IsDecal: true };

			public VariationOverride(AssetMaterialVariation variation, AssetMaterialOverride @override)
			{
				Variation = variation;
				Override = @override;
			}

			public bool Equals(VariationOverride other) => Variation == other.Variation && Override.Id == other.Override.Id;

			public override bool Equals(object obj) => obj is VariationOverride other && Equals(other);

			public override int GetHashCode() => HashCode.Combine(Variation, Override.Id);
		}

		public readonly struct VariationWithDefault
		{
			public readonly AssetMaterialTarget Target;
			public readonly string Name;

			public VariationWithDefault(AssetMaterialTarget target, string name)
			{
				Target = target;
				Name = name;
			}
		}
	}
}
