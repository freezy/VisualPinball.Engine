// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity.Editor
{
	public static class SpinnerExtensions
	{
		internal static IVpxPrefab InstantiatePrefab(this Spinner spinner)
		{
			var prefab = RenderPipeline.Current.PrefabProvider.CreateSpinner();
			return new VpxPrefab<Spinner, SpinnerData, SpinnerComponent>(prefab, spinner);
		}
	}
}
