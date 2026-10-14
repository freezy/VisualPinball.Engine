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
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The cabinet screens a <see cref="CabinetScreenComponent"/> can show, matching the <see cref="SurfaceRoles"/>.
	/// </summary>
	public enum CabinetScreenRole
	{
		Backglass,
		Topper,
		Dmd,
		FullDmd,
		Custom,
	}

	/// <summary>
	/// Marks a surface of the cabinet, such as the backglass or the topper screen, on which a plugin's screen is shown
	/// while the game runs.
	/// </summary>
	/// <remarks>
	/// VPE shows the surface a plugin publishes for <see cref="Role"/> on the <see cref="Target"/> renderer, by writing it
	/// into a texture property of the target's material. The authored material therefore defines the look of the screen,
	/// and is what remains when no plugin provides the screen. In that case, <see cref="HideRoot"/> is deactivated if set,
	/// so for example a topper disappears entirely when there is no PUP pack. The material slot's property block belongs to
	/// the screen, so don't drive the same slot from other components such as lights.
	///
	/// The <see cref="CabinetScreenRole.Dmd"/> role only shows a plugin's DMD when the gamelogic engine announces no DMD
	/// of its own. The engine's DMD counts as the screen's source, so the hide root of a DMD screen may contain the table's
	/// <see cref="DotMatrixDisplayComponent"/>.
	/// </remarks>
	[PackAs("CabinetScreen")]
	[AddComponentMenu("Pinball/Display/Cabinet Screen")]
	public class CabinetScreenComponent : MonoBehaviour, IPackable
	{
		[Tooltip("Which cabinet screen is shown on this surface.")]
		public CabinetScreenRole Role = CabinetScreenRole.Backglass;

		[Tooltip("The role name a plugin publishes the screen with, such as \"pup-screen-8\". Only used when Role is Custom.")]
		public string CustomRole = string.Empty;

		[Tooltip("The renderer showing the screen. Defaults to the renderer of this GameObject.")]
		public Renderer Target;

		[Tooltip("The material slot of the target renderer that shows the screen.")]
		public int MaterialIndex;

		[Tooltip("The texture property of the material the screen is written to. Leave empty to use the unlit color map, the emissive color map if the material uses one, or the base color map.")]
		public string TextureProperty = string.Empty;

		[Tooltip("Deactivated when no plugin provides this screen, such as the whole topper. Leave empty to keep the authored material instead.")]
		public GameObject HideRoot;

		/// <summary>
		/// The <see cref="PluginResourceDescriptor.Role"/> of the surfaces shown here.
		/// </summary>
		public string RoleName => Role switch {
			CabinetScreenRole.Backglass => SurfaceRoles.Backglass,
			CabinetScreenRole.Topper => SurfaceRoles.Topper,
			CabinetScreenRole.Dmd => SurfaceRoles.Dmd,
			CabinetScreenRole.FullDmd => SurfaceRoles.FullDmd,
			_ => CustomRole?.Trim() ?? string.Empty,
		};

		/// <summary>
		/// The renderer showing the screen: <see cref="Target"/>, or the renderer of this GameObject.
		/// </summary>
		public Renderer TargetRenderer => Target != null ? Target : GetComponent<Renderer>();

		/// <summary>
		/// Sets <see cref="Role"/> and <see cref="CustomRole"/> from a role name.
		/// </summary>
		public void SetRoleName(string role)
		{
			switch (role) {
				case SurfaceRoles.Backglass: Role = CabinetScreenRole.Backglass; break;
				case SurfaceRoles.Topper: Role = CabinetScreenRole.Topper; break;
				case SurfaceRoles.Dmd: Role = CabinetScreenRole.Dmd; break;
				case SurfaceRoles.FullDmd: Role = CabinetScreenRole.FullDmd; break;
				default:
					Role = CabinetScreenRole.Custom;
					CustomRole = role ?? string.Empty;
					break;
			}
		}

		#region Packaging

		public byte[] Pack() => CabinetScreenPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => CabinetScreenReferencesPackable.Pack(this, refs);

		public void Unpack(byte[] bytes) => CabinetScreenPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) => CabinetScreenReferencesPackable.Unpack(data, this, refs);

		#endregion
	}
}
