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
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Root-level <c>manifest.json</c> of a .vpe package.
	///
	/// The manifest is the entry point for any reader: it identifies the file as a VPE table
	/// package, declares the container format version, the schema version of each payload, and
	/// the component types a full restore requires. A file without a manifest is not a valid
	/// .vpe package; readers refuse it.
	///
	/// Versioning policy: <see cref="FormatVersion"/> only changes when the container layout
	/// changes incompatibly (folder structure, node addressing). Individual payload schemas
	/// evolve independently through <see cref="Schemas"/>; readers must check the schema version
	/// of a payload before parsing it and skip (with a warning) versions they don't know.
	/// </summary>
	[Serializable]
	public class VpePackageManifest
	{
		/// <summary>Container format version. See <see cref="PackageApi.FormatVersion"/>.</summary>
		public int FormatVersion = PackageApi.FormatVersion;

		/// <summary>Free-form identifier of the writing tool, for diagnostics only.</summary>
		public string WrittenBy;

		/// <summary>
		/// Node id (glTF <c>extras.vpeId</c> in table.glb) of the table root. Readers anchor the
		/// restored component hierarchy here instead of guessing the root from the scene.
		/// </summary>
		public string RootNodeId;

		/// <summary>Schema version per payload, keyed by <see cref="VpePackageSchemas"/> names.</summary>
		public Dictionary<string, int> Schemas = new();

		/// <summary>
		/// PackAs names of all component and asset types stored in items/, refs/ and assets/.
		/// Readers compare this against their registered types and can warn up front about
		/// missing plugins instead of failing mid-restore.
		/// </summary>
		public List<string> ComponentTypes = new();
	}

	public static class VpePackageSchemas
	{
		public const string Items = "items";
		public const string Materials = "materials";
		public const string Lights = "lights";
		public const string Sounds = "sounds";
	}

	public static class VpePackageManifestIo
	{
		public static void Write(IPackageStorage storage, VpePackageManifest manifest)
		{
			storage.AddFile(PackageApi.ManifestFile, PackageApi.Packer.FileExtension)
				.SetData(PackageApi.Packer.Pack(manifest));
		}

		public static VpePackageManifest TryRead(IPackageStorage storage)
		{
			if (!storage.TryGetFile(PackageApi.ManifestFile, out var file, PackageApi.Packer.FileExtension)) {
				return null;
			}
			try {
				return PackageApi.Packer.Unpack<VpePackageManifest>(file.GetData());
			} catch (Exception) {
				return null;
			}
		}
	}
}
