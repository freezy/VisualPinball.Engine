// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using System.Linq;

namespace VisualPinball.Unity.Editor
{
	internal class AssetQuery
	{
		public event EventHandler<AssetQueryResult> OnQueryUpdated;

		private readonly List<AssetLibrary> _libraries;

		public AssetQuery(List<AssetLibrary> libraries)
		{
			_libraries = libraries;
		}

		public void Toggle(AssetLibrary lib, bool enabled)
		{
			if (enabled && !_libraries.Contains(lib)) {
				_libraries.Add(lib);
			}
			if (!enabled && _libraries.Contains(lib)) {
				_libraries.Remove(lib);
			}

			Run();
		}

		public void Run()
		{
			var assets = _libraries.SelectMany(lib => lib.GetAssets()).ToList();
			OnQueryUpdated?.Invoke(this, new AssetQueryResult(assets));
		}

		public List<LibraryAsset> All => _libraries.SelectMany(lib => lib.GetAssets()).ToList();
	}
}
