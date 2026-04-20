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
	// The contract between a .vpe reader (portable) and the Player app (pipeline-specific).
	//
	// A resolver turns a VpeMaterialProfileV1 + its textures into a live Unity Material rendered by
	// whatever shader the Player owns at its own build time. This is the only place HDRP/URP/custom
	// SRP specifics are allowed to appear on the reader side.
	//
	// Registration is static because RuntimePackageReader is a plain class instantiated by gameplay
	// code; the Player registers its resolver once at bootstrap (a MonoBehaviour on the player scene
	// works well).
	public interface IVpeMaterialResolver
	{
		// Returns true if this resolver knows how to build a material for the given profile type.
		// See VpeMaterialTypes.
		bool Supports(string materialType);

		// Build a Unity Material from the portable profile. The importedMaterial is the material
		// that the glTF import produced on the renderer slot; resolvers may sample it for fallback
		// values but should not return it directly (the whole point is to replace it).
		// Return null if the resolver cannot produce a material for this profile.
		Material CreateMaterial(VpeMaterialProfileV1 profile, IVpeTextureProvider textures, Material importedMaterial);
	}

	public interface IVpeTextureProvider
	{
		// Returns the runtime Texture2D for the given id, or null if the id is unknown / empty.
		// Textures are materialized lazily on first request and cached for the lifetime of the
		// provider (which lives as long as the import).
		Texture2D Get(string textureId);
	}

	public static class VpeMaterialResolver
	{
		private static IVpeMaterialResolver _active;

		// Register a resolver for the current process. A null argument clears the registration.
		// The last registration wins — tests or the Player bootstrap should call this once.
		public static void Register(IVpeMaterialResolver resolver)
		{
			_active = resolver;
		}

		public static IVpeMaterialResolver Active => _active;
	}
}
