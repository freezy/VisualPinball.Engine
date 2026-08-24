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
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	[PackAs("RubberGuide")]
	[AddComponentMenu("Pinball/Rubber/Rubber Guide")]
	public sealed class RubberGuideComponent : MonoBehaviour, IPackable,
		ISerializationCallbackReceiver
	{
		public const float DefaultSlotRadius = 0.01f;

		[SerializeField] private RubberGuideSlot[] _slots = Array.Empty<RubberGuideSlot>();

		public RubberGuideSlot[] Slots {
			get => _slots ?? Array.Empty<RubberGuideSlot>();
			set => _slots = NormalizeSlots(value);
		}

		public bool TryGetSlot(SerializedGuid id, out RubberGuideSlot slot)
		{
			foreach (var candidate in Slots) {
				if (candidate.Id == id) {
					slot = candidate;
					return true;
				}
			}
			slot = default;
			return false;
		}

		public IReadOnlyList<string> ValidateSlots()
		{
			var errors = new List<string>();
			var ids = new HashSet<SerializedGuid>();
			for (var i = 0; i < Slots.Length; i++) {
				var slot = Slots[i];
				if (slot.Id.IsEmpty) {
					errors.Add($"Slot {i} has no ID.");
				} else if (!ids.Add(slot.Id)) {
					errors.Add($"Slot {i} duplicates ID {slot.Id}.");
				}
				if (!float.IsFinite(slot.LocalHeight)) {
					errors.Add($"Slot {i} has a non-finite height.");
				}
				if (slot.Profile.Type != RubberGuideProfileType.Circle) {
					errors.Add($"Slot {i} uses unsupported profile {slot.Profile.Type}.");
				} else if (!float.IsFinite(slot.Profile.Radius) || slot.Profile.Radius <= 0f) {
					errors.Add($"Slot {i} must have a positive finite radius.");
				}
			}
			return errors;
		}

		public void AddSlot(RubberGuideSlot slot)
		{
			if (slot.Id.IsEmpty) {
				slot.Id = SerializedGuid.New();
			}
			_slots = Slots.Concat(new[] { NormalizeSlot(slot) }).ToArray();
		}

		public bool EnsureDefaultSlot()
		{
			if (Slots.Length > 0) {
				return false;
			}
			AddSlot(RubberGuideSlot.Create("Default", DefaultSlotRadius));
			return true;
		}

		public void DuplicateSlot(int index)
		{
			if (index < 0 || index >= Slots.Length) {
				throw new ArgumentOutOfRangeException(nameof(index));
			}
			var slot = Slots[index];
			slot.Id = SerializedGuid.New();
			slot.DisplayName = string.IsNullOrEmpty(slot.DisplayName) ? "Slot" : $"{slot.DisplayName} Copy";
			AddSlot(slot);
		}

		public int RepairDuplicateIds()
		{
			var repaired = 0;
			var ids = new HashSet<SerializedGuid>();
			var slots = Slots.ToArray();
			for (var i = 0; i < slots.Length; i++) {
				if (!slots[i].Id.IsEmpty && ids.Add(slots[i].Id)) {
					continue;
				}
				slots[i].Id = SerializedGuid.New();
				ids.Add(slots[i].Id);
				repaired++;
			}
			_slots = slots;
			return repaired;
		}

		private void Reset()
		{
			EnsureDefaultSlot();
		}

		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize()
		{
			_slots = NormalizeSlots(_slots);
		}

		private static RubberGuideSlot[] NormalizeSlots(RubberGuideSlot[] slots)
		{
			return slots?.Select(NormalizeSlot).ToArray() ?? Array.Empty<RubberGuideSlot>();
		}

		internal static RubberGuideSlot NormalizeSlot(RubberGuideSlot slot)
		{
			if (slot.Profile.Type == RubberGuideProfileType.Circle) {
				return slot;
			}

			var profile = slot.Profile;
			var migratedRadius = 0f;
			if (profile.ConvexHull != null) {
				foreach (var point in profile.ConvexHull) {
					if (math.all(math.isfinite(point))
						&& math.all(math.isfinite(profile.LocalCenter))) {
						migratedRadius = math.max(migratedRadius,
							math.distance(point, profile.LocalCenter));
					}
				}
			}
			if (!float.IsFinite(migratedRadius) || migratedRadius <= 0f) {
				migratedRadius = float.IsFinite(profile.Radius) && profile.Radius > 0f
					? profile.Radius
					: DefaultSlotRadius;
			}
			profile.Type = RubberGuideProfileType.Circle;
			profile.Radius = migratedRadius;
			profile.ConvexHull = Array.Empty<float2>();
			slot.Profile = profile;
			return slot;
		}

		public byte[] Pack() => RubberGuidePackable.Pack(this);
		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => Array.Empty<byte>();
		public void Unpack(byte[] bytes) => RubberGuidePackable.Unpack(bytes, this);
		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }
	}

	public struct RubberGuidePackable
	{
		public RubberGuideSlotPackable[] Slots;

		public static byte[] Pack(RubberGuideComponent component)
		{
			return PackageApi.Packer.Pack(new RubberGuidePackable {
				Slots = component.Slots.Select(RubberGuideSlotPackable.From).ToArray(),
			});
		}

		public static void Unpack(byte[] bytes, RubberGuideComponent component)
		{
			var data = PackageApi.Packer.Unpack<RubberGuidePackable>(bytes);
			component.Slots = data.Slots?.Select(slot => slot.ToSlot()).ToArray()
				?? Array.Empty<RubberGuideSlot>();
		}
	}

	public struct RubberGuideSlotPackable
	{
		public ulong IdA;
		public ulong IdB;
		public string DisplayName;
		public float LocalHeight;
		public int ProfileType;
		public PackableFloat2 LocalCenter;
		public float Radius;
		public PackableFloat2[] ConvexHull;
		public float RubberSupportFriction;

		public static RubberGuideSlotPackable From(RubberGuideSlot slot)
		{
			return new RubberGuideSlotPackable {
				IdA = slot.Id.A,
				IdB = slot.Id.B,
				DisplayName = slot.DisplayName,
				LocalHeight = slot.LocalHeight,
				ProfileType = (int)slot.Profile.Type,
				LocalCenter = slot.Profile.LocalCenter,
				Radius = slot.Profile.Radius,
				ConvexHull = slot.Profile.ConvexHull?.Select(point => (PackableFloat2)point).ToArray()
					?? Array.Empty<PackableFloat2>(),
				RubberSupportFriction = slot.RubberSupportFriction,
			};
		}

		public RubberGuideSlot ToSlot()
		{
			return new RubberGuideSlot {
				Id = new SerializedGuid(IdA, IdB),
				DisplayName = DisplayName,
				LocalHeight = LocalHeight,
				Profile = new RubberGuideProfile {
					Type = (RubberGuideProfileType)ProfileType,
					LocalCenter = LocalCenter,
					Radius = Radius,
					ConvexHull = ConvexHull?.Select(point => (global::Unity.Mathematics.float2)point).ToArray()
						?? Array.Empty<global::Unity.Mathematics.float2>(),
				},
				RubberSupportFriction = RubberSupportFriction,
			};
		}
	}
}
