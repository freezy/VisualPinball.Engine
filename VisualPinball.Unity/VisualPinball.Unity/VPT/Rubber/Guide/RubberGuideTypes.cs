// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	[Serializable]
	public struct SerializedGuid : IEquatable<SerializedGuid>
	{
		[SerializeField] private ulong _a;
		[SerializeField] private ulong _b;

		public ulong A => _a;
		public ulong B => _b;
		public bool IsEmpty => _a == 0UL && _b == 0UL;

		public SerializedGuid(ulong a, ulong b)
		{
			_a = a;
			_b = b;
		}

		public static SerializedGuid New()
		{
			var bytes = Guid.NewGuid().ToByteArray();
			return new SerializedGuid(BitConverter.ToUInt64(bytes, 0), BitConverter.ToUInt64(bytes, 8));
		}

		public bool Equals(SerializedGuid other) => _a == other._a && _b == other._b;
		public override bool Equals(object obj) => obj is SerializedGuid other && Equals(other);
		public override int GetHashCode()
		{
			unchecked {
				return (_a.GetHashCode() * 397) ^ _b.GetHashCode();
			}
		}

		public override string ToString() => $"{_a:x16}{_b:x16}";

		public static bool operator ==(SerializedGuid left, SerializedGuid right) => left.Equals(right);
		public static bool operator !=(SerializedGuid left, SerializedGuid right) => !left.Equals(right);
	}

	public enum RubberGuideProfileType
	{
		Circle,
		Convex2D,
	}

	[Serializable]
	public struct RubberGuideProfile
	{
		public RubberGuideProfileType Type;
		public float2 LocalCenter;
		public float Radius;
		public float2[] ConvexHull;

		public static RubberGuideProfile Circle(float radius)
		{
			return new RubberGuideProfile {
				Type = RubberGuideProfileType.Circle,
				LocalCenter = float2.zero,
				Radius = radius,
				ConvexHull = Array.Empty<float2>(),
			};
		}
	}

	[Serializable]
	public struct RubberGuideSlot
	{
		public SerializedGuid Id;
		public string DisplayName;
		public float LocalHeight;
		public RubberGuideProfile Profile;
		public float RubberSupportFriction;

		public static RubberGuideSlot Create(string displayName, float radius)
		{
			return new RubberGuideSlot {
				Id = SerializedGuid.New(),
				DisplayName = displayName,
				Profile = RubberGuideProfile.Circle(radius),
			};
		}
	}

	[Serializable]
	public struct RubberGuideBinding
	{
		public RubberGuideComponent Guide;
		public SerializedGuid SlotId;

		public RubberGuideBinding(RubberGuideComponent guide, SerializedGuid slotId)
		{
			Guide = guide;
			SlotId = slotId;
		}
	}
}
