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

using System.Collections.Generic;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Combines the physical state of an input action with synthetic presses,
	/// so that only edges of the combined state reach switches and wires.
	/// </summary>
	/// <remarks>
	/// The synthetic source is the auto plunger's Launch Ball press: a
	/// pull-and-release of the analog plunger holds Launch Ball for 100 ms. If the
	/// player holds the real button at the same time, releasing either one must
	/// not release the other.
	/// </remarks>
	internal sealed class InputActionHoldAggregator
	{
		private readonly Dictionary<string, bool> _physical = new();
		private readonly Dictionary<string, bool> _synthetic = new();

		public bool IsPressed(string action)
		{
			return Get(_physical, action) || Get(_synthetic, action);
		}

		/// <summary>
		/// Records a physical edge.
		/// </summary>
		/// <returns>True if the combined state changed to <paramref name="effective"/>.</returns>
		public bool SetPhysical(string action, bool pressed, out bool effective)
		{
			return Set(_physical, action, pressed, out effective);
		}

		/// <summary>
		/// Records a synthetic hold or release.
		/// </summary>
		/// <returns>True if the combined state changed to <paramref name="effective"/>.</returns>
		public bool SetSynthetic(string action, bool held, out bool effective)
		{
			return Set(_synthetic, action, held, out effective);
		}

		private bool Set(Dictionary<string, bool> map, string action, bool value, out bool effective)
		{
			var before = IsPressed(action);
			map[action] = value;
			effective = IsPressed(action);
			return before != effective;
		}

		private static bool Get(Dictionary<string, bool> map, string action)
		{
			return action != null && map.TryGetValue(action, out var value) && value;
		}
	}

	/// <summary>
	/// Turns the published auto plunger Launch Ball state into hold edges.
	/// </summary>
	/// <remarks>
	/// The physics side publishes a press counter and whether a press is held.
	/// A counter that changed while nothing is held is a press that started and
	/// ended between two reads, which is reported as a pulse.
	/// </remarks>
	internal struct AutoPlungerLaunchTracker
	{
		public enum Change
		{
			None,
			Press,
			Release,
			/// <summary>Press and release, both happened since the last read.</summary>
			Pulse
		}

		private int _lastCount;
		private bool _held;
		private bool _initialized;

		public bool IsHeld => _held;

		public Change Update(int count, bool held)
		{
			if (!_initialized) {
				// Presses before the first read belong to nobody, only a current hold counts.
				_initialized = true;
				_lastCount = count;
				_held = held;
				return held ? Change.Press : Change.None;
			}

			var newPress = count != _lastCount;
			_lastCount = count;
			if (held != _held) {
				_held = held;
				return held ? Change.Press : Change.Release;
			}
			return !held && newPress ? Change.Pulse : Change.None;
		}

		/// <summary>
		/// Forgets the hold, e.g. when another consumer takes over.
		/// </summary>
		public Change Reset()
		{
			var wasHeld = _held;
			_held = false;
			_initialized = false;
			return wasHeld ? Change.Release : Change.None;
		}
	}
}
