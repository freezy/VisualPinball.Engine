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
		#region Public Members

		public readonly Asset Asset;
		public readonly VariationOverride[] Overrides;

		public string DecalVariationNames =>
			string.Join("|", Overrides.Where(o => o.Variation.IsDecal).Select(o => o.Override.VariationName).OrderBy(name => name));

		public string ThumbId => GenerateThumbID();
		public string ThumbPath => $"{Asset.Library.ThumbnailRoot}/{ThumbId}.webp";

		public bool IsOriginal => Overrides.Length == 0;

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

		#endregion

		#region Private Members

		private string _thumbId;

		public string Name => string.Join(", ",
			(Overrides.Any(v => v.IsDecal)
				? Overrides.Where(v => v.IsDecal)
				: Overrides
			).Select(v => $"{v.Override.Name} {v.Variation.Name}")
		);

		public string MaterialName => string.Join(", ",
			Overrides.Where(v => !v.IsDecal).Select(v => $"{v.Override.Name} {v.Variation.Name}")
		);


		private VariationWithDefault[] VariationsWithDefaults =>
			Overrides
				.Select(v => new VariationWithDefault(v.Variation.Target, v.Override.Name))
				.Concat(Asset.MaterialDefaults
					.Where(md => Overrides.All(v => v.Variation.Target != md.Target))
					.Select(md => new VariationWithDefault(md.Target, md.Name)))
				.ToArray();

		#endregion

		#region Constructors

		public AssetMaterialCombination(Asset asset)
		{
			Asset = asset;
			Overrides = Array.Empty<VariationOverride>();
		}

		internal AssetMaterialCombination(Asset asset, IReadOnlyList<Counter> counters, IReadOnlyList<AssetMaterialVariation> variations)
		{
			Asset = asset;
			Overrides = new VariationOverride[counters.Count];
			for (var i = 0; i < counters.Count; i++) {
				var overrideIndex = counters[i].Value;
				Overrides[i] = new VariationOverride(
					overrideIndex == 0 ? null : variations[i],
					overrideIndex == 0 ? null : variations[i].Overrides[overrideIndex - 1]
				);
			}
			Overrides = Overrides.Where(mv => mv.Variation != null).ToArray();
		}

		#endregion

		public AssetMaterialCombination GetValidCombination()
		{
			if (IsValidCombination) {
				return this;
			}

			return Asset.GetCombinations(true, true)
				.Where(c => c.Matches(this))
				.FirstOrDefault(c => c.IsValidCombination);
		}

		/// <summary>
		/// Returns whether the overrides of the given <see cref="AssetMaterialCombination"/> match the overridden targets
		/// of this combination. Name is ignored. If the given combination is null, then this combination is considered to
		/// match if it has no overrides.
		/// </summary>
		/// <param name="materialCombination">Combination to compare with</param>
		/// <returns>True of overrides of combination have the same targets, false otherwise.</returns>
		public bool EqualsOverrides(AssetMaterialCombination materialCombination)
		{
			if (materialCombination == null && Overrides.Length == 0) {
				return true;
			}
			if (materialCombination == null || Overrides.Length != materialCombination.Overrides.Length) {
				return false;
			}

			return Overrides
				.Select(vo => materialCombination.Overrides
					.FirstOrDefault(o => o.Variation.Target == vo.Variation.Target))
				.All(matchingOverride => matchingOverride != null);
		}

		private bool Matches(AssetMaterialCombination combination)
		{
			if (combination == null || combination.Overrides.Length == 0) {
				return false;
			}
			return combination.Overrides.All(ovr => Overrides.Contains(ovr));
		}

		/// <summary>
		/// Checks whether a given <see cref="AssetMaterialDefault"/> matches this combination, i.e.
		/// if there is an override for the target of the default, and if that override's name matches.
		///
		/// If there is no override for the target, then the default is considered to match.
		/// </summary>
		/// <param name="md"></param>
		/// <returns></returns>
		public bool Matches(AssetMaterialDefault md)
		{
			if (md == null || md.Target == null) {
				return false;
			}
			var o = Overrides.FirstOrDefault(o => o.Variation.Target == md.Target);
			if (o == null) {
				return true;
			}
			return o.Override.Name == md.Name;
		}

		/// <summary>
		/// Same as <see cref="Matches(AssetMaterialDefault)"/>, but for an <see cref="VariationOverride"/>.
		/// </summary>
		/// <param name="vo"></param>
		/// <returns></returns>
		public bool Matches(VariationOverride vo)
		{
			if (vo == null || vo.Variation.Target == null) {
				return false;
			}
			var o = Overrides.FirstOrDefault(o => o.Variation.Target == vo.Variation.Target);
			if (o == null) {
				return false;
			}
			return o.Override.Name == vo.Override.Name;
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
			foreach (var v in Overrides) {
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

			if (Overrides.Length == 0) {
				_thumbId = Asset.GUID;

			} else {
				const int byteCount = 16;
				var guid1 = new Guid(Asset.GUID);
				foreach (var v in Overrides) {
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

		public override string ToString() => $"{string.Join(" | ", Overrides.Select(v => $"{v.Override.Name} {v.Variation.Name} ({v.Override.Material.name})"))}";

		/// <summary>
		/// This class represents an Override with a link to its Variation.
		///
		/// (We used tuples for this in the past which turned out to be not very readable.)
		/// </summary>
		public class VariationOverride : IEquatable<VariationOverride>
		{
			public readonly AssetMaterialVariation Variation;
			public readonly AssetMaterialOverride Override;
			public bool IsDecal => Variation is { IsDecal: true };

			public VariationOverride(AssetMaterialVariation variation, AssetMaterialOverride @override)
			{
				Variation = variation;
				Override = @override;
			}

			public override string ToString() => $"{Variation?.Name ?? "<null>"}: {Override?.Name ?? "<null>"}";

			public bool Equals(VariationOverride other) => Equals(Variation, other?.Variation) && Override?.Id == other?.Override?.Id;

			public override bool Equals(object obj) => obj is VariationOverride other && Equals(other);

			public override int GetHashCode() => HashCode.Combine(Variation, Override?.Id);
		}

		private readonly struct VariationWithDefault
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
