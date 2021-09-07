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

using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	internal interface IVpxPrefab
	{
		GameObject GameObject { get; }

		IItemMainAuthoring MainComponent { get; }

		MeshFilter[] MeshFilters { get; }

		bool ExtractMesh { get; }

		bool SkipParenting { get; }

		void SetData();

		public void SetReferencedData(Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider,
			Dictionary<string, IItemMainAuthoring> components);

		public void PersistData();

		public void UpdateTransforms();

		void FreeBinaryData();
	}
}
