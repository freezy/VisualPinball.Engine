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
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public interface IPatcher
	{
		void Set(FileTableContainer tableContainer, string filename, IMaterialProvider materialProvider, ITextureProvider textureProvider);
		void ApplyPatches(GameObject gameObject, GameObject tableGameObject);
		void PostPatch(GameObject tableGameObject);
	}

	public static class PatcherManager
	{
		public static IPatcher GetPatcher() {
			var t = typeof(IPatcher);
			var patchers = AppDomain.CurrentDomain.GetAssemblies()
				.Where(x => x.FullName.StartsWith("VisualPinball."))
				.SelectMany(x => x.GetTypes())
				.Where(x => x.IsClass && t.IsAssignableFrom(x))
				.Select(x => (IPatcher) Activator.CreateInstance(x))
				.ToArray();

			return patchers.First();
		}
	}
}
