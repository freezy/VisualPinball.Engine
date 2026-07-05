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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Unity asset wrapper for cabinet input defaults or named cabinet profiles.
	/// </summary>
	/// <remarks>
	/// Built players should still persist user edits to JSON. The asset exists as
	/// an editor-friendly default/preset container and can be written during Play
	/// Mode in the editor when a human intentionally wants to update the preset.
	/// </remarks>
	[CreateAssetMenu(menuName = "Visual Pinball/Cabinet Input Settings", fileName = "CabinetInputSettings")]
	public sealed class CabinetInputSettingsAsset : ScriptableObject
	{
		public CabinetInputSettings settings = new();

		/// <summary>
		/// Applies this asset's settings to a loaded table root.
		/// </summary>
		public void ApplyTo(GameObject tableRoot)
		{
			settings ??= new CabinetInputSettings();
			settings.ApplyTo(tableRoot);
		}

		private void OnValidate()
		{
			settings ??= new CabinetInputSettings();
			settings.Normalize();
		}
	}
}
