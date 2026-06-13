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

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// One progress update from a package export: a stage-weighted completion fraction in [0,1] and a
	/// short human-readable label. Reported through <c>IProgress&lt;ExportProgress&gt;</c>; the writer
	/// may report from worker threads, so a <c>Progress&lt;T&gt;</c> handler marshals these back to the
	/// main thread for UI.
	/// </summary>
	public readonly struct ExportProgress
	{
		public ExportProgress(float fraction, string message)
		{
			Fraction = fraction;
			Message = message;
		}

		/// <summary>Completion fraction in [0,1], weighted across export stages.</summary>
		public float Fraction { get; }

		/// <summary>Short label for the current stage (e.g. "Loading textures (12/200)…").</summary>
		public string Message { get; }
	}
}
