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

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Quarter-turn mounting options for accelerometer boards installed in a
	/// cabinet.
	/// </summary>
	public enum NudgeSensorMountRotation
	{
		[InspectorName("0 deg")]
		Rotation0 = 0,

		[InspectorName("90 deg")]
		Rotation90 = 1,

		[InspectorName("180 deg")]
		Rotation180 = 2,

		[InspectorName("270 deg")]
		Rotation270 = 3
	}

	/// <summary>
	/// Converts sensor-space axes to VPE cabinet-space axes.
	/// </summary>
	/// <remarks>
	/// KL25Z/Pinscape-style boards are often mounted in whichever orientation is
	/// convenient inside the cabinet. Keeping this as a pure transform lets
	/// calibration, graphing, and physics all consume the same canonical X/Y
	/// convention.
	/// </remarks>
	public static class NudgeSensorMountTransform
	{
		/// <summary>
		/// Normalizes invalid serialized enum values back to no rotation.
		/// </summary>
		public static NudgeSensorMountRotation NormalizeRotation(NudgeSensorMountRotation rotation)
		{
			return (uint)rotation <= (uint)NudgeSensorMountRotation.Rotation270
				? rotation
				: NudgeSensorMountRotation.Rotation0;
		}

		/// <summary>
		/// Applies mirror and rotation to a two-axis sensor vector.
		/// </summary>
		public static float2 Transform(float2 value, NudgeSensorMountRotation rotation, bool mirrorX)
		{
			if (mirrorX) {
				value.x = -value.x;
			}

			switch (NormalizeRotation(rotation)) {
				case NudgeSensorMountRotation.Rotation90:
					return new float2(-value.y, value.x);
				case NudgeSensorMountRotation.Rotation180:
					return -value;
				case NudgeSensorMountRotation.Rotation270:
					return new float2(value.y, -value.x);
				default:
					return value;
			}
		}

		/// <summary>
		/// Applies the mount transform to a single channel and value.
		/// </summary>
		/// <remarks>
		/// Samples arrive one native axis at a time, so this method rotates both the
		/// channel identity and its sign instead of requiring callers to assemble a
		/// full vector first.
		/// </remarks>
		public static void TransformChannel(ref NudgeSensorChannel channel, ref float value,
			NudgeSensorMountRotation rotation, bool mirrorX)
		{
			if (!TryGetChannelAxis(channel, out var sourceX, out var group)) {
				return;
			}

			var basis = sourceX ? new float2(1f, 0f) : new float2(0f, 1f);
			var transformed = Transform(basis, rotation, mirrorX);
			var targetX = math.abs(transformed.x) > math.abs(transformed.y);
			value *= targetX ? transformed.x : transformed.y;
			channel = ChannelFor(group, targetX);
		}

		/// <summary>
		/// Swaps mapped-axis flags when the mount rotation swaps X and Y.
		/// </summary>
		public static void TransformMappedAxes(ref bool xMapped, ref bool yMapped,
			NudgeSensorMountRotation rotation)
		{
			switch (NormalizeRotation(rotation)) {
				case NudgeSensorMountRotation.Rotation90:
				case NudgeSensorMountRotation.Rotation270:
					(xMapped, yMapped) = (yMapped, xMapped);
					break;
			}
		}

		private static bool TryGetChannelAxis(NudgeSensorChannel channel, out bool sourceX, out ChannelGroup group)
		{
			switch (channel) {
				case NudgeSensorChannel.X:
					sourceX = true;
					group = ChannelGroup.Position;
					return true;
				case NudgeSensorChannel.Y:
					sourceX = false;
					group = ChannelGroup.Position;
					return true;
				case NudgeSensorChannel.VelocityX:
					sourceX = true;
					group = ChannelGroup.Velocity;
					return true;
				case NudgeSensorChannel.VelocityY:
					sourceX = false;
					group = ChannelGroup.Velocity;
					return true;
				case NudgeSensorChannel.AccelerationX:
					sourceX = true;
					group = ChannelGroup.Acceleration;
					return true;
				case NudgeSensorChannel.AccelerationY:
					sourceX = false;
					group = ChannelGroup.Acceleration;
					return true;
				default:
					sourceX = false;
					group = default;
					return false;
			}
		}

		private static NudgeSensorChannel ChannelFor(ChannelGroup group, bool x)
		{
			return group switch {
				ChannelGroup.Velocity => x ? NudgeSensorChannel.VelocityX : NudgeSensorChannel.VelocityY,
				ChannelGroup.Acceleration => x ? NudgeSensorChannel.AccelerationX : NudgeSensorChannel.AccelerationY,
				_ => x ? NudgeSensorChannel.X : NudgeSensorChannel.Y
			};
		}

		private enum ChannelGroup
		{
			Position,
			Velocity,
			Acceleration
		}
	}
}
