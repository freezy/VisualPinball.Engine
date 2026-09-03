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

namespace VisualPinball.Unity
{
	[PackAs("RubberGuide")]
	[AddComponentMenu("Pinball/Rubber/Rubber Guide")]
	public sealed class RubberGuideComponent : MonoBehaviour, IPackable
	{
		[SerializeField] private RubberGuideSlot[] _slots = Array.Empty<RubberGuideSlot>();

		public RubberGuideSlot[] Slots {
			get => _slots ?? Array.Empty<RubberGuideSlot>();
			set => _slots = value ?? Array.Empty<RubberGuideSlot>();
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
			_slots = Slots.Concat(new[] { slot }).ToArray();
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
