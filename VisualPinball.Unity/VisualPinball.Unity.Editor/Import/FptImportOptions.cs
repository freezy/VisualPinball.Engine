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

using System;

namespace VisualPinball.Unity.Editor
{
	[Serializable]
	public sealed class FptImportOptions
	{
		public string AssetRoot = "Assets/Tables";
		public string[] LibrarySearchRoots = Array.Empty<string>();
		public bool CopyOriginalTable = true;
		public bool OverwriteChangedSourceFiles;
		public bool ReuseGeneratedAssets = true;
		public bool ReplaceExistingSceneRoot = true;
		public bool ImportPrimaryModels = true;
		public bool GenerateColliders = true;
		public bool EnablePerPolygonCollision = true;
		public bool GenerateRenderMeshFallbackColliders;
	}
}
