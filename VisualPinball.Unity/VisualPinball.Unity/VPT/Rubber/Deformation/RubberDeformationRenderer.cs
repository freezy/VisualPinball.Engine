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
	/// Displaces the render mesh of a deforming Physical rubber with the same modal
	/// basis the physics uses.
	/// </summary>
	/// <remarks>
	/// This is the always-compiled shell. It carries the authored settings and packs
	/// as ordinary component data in every configuration, so a table exported with
	/// the experimental scripting define loads without missing scripts and without
	/// data loss in a build that does not have it. The runtime body — vertex
	/// binding, mesh instancing, per-frame displacement and normal recalculation —
	/// arrives in a later commit of this series, behind
	/// <c>VPE_EXPERIMENTAL_RUBBER_DEFORMATION</c>.
	/// </remarks>
	[PackAs("RubberDeformationRenderer")]
	[AddComponentMenu("Pinball/Game Item/Rubber Deformation Renderer")]
	[DisallowMultipleComponent]
	public sealed class RubberDeformationRenderer : MonoBehaviour, IPackable
	{
		public const float DefaultUpdateThreshold = 0.05f;

		[SerializeField]
		[Min(0f)]
		[Tooltip("Smallest modal amplitude change in VPX units that rebuilds the render mesh. Zero updates every frame.")]
		private float _updateThreshold = DefaultUpdateThreshold;

		[SerializeField]
		[Tooltip("Recalculate mesh normals after displacing the vertices. Turning this off is cheaper but flattens the shading of a strongly deflected cord.")]
		private bool _recalculateNormals = true;

		[SerializeField]
		[Tooltip("Displace the render vertices of supported arcs as well. Supported arcs do not move in phase 3b, so this only matters once arcs may lift off their guides.")]
		private bool _includeSupportedArcs;

		public float UpdateThreshold {
			get => float.IsFinite(_updateThreshold) && _updateThreshold >= 0f
				? _updateThreshold
				: DefaultUpdateThreshold;
			set => _updateThreshold = float.IsFinite(value) && value >= 0f
				? value
				: DefaultUpdateThreshold;
		}

		public bool RecalculateNormals {
			get => _recalculateNormals;
			set => _recalculateNormals = value;
		}

		public bool IncludeSupportedArcs {
			get => _includeSupportedArcs;
			set => _includeSupportedArcs = value;
		}

		/// <summary>
		/// False until the deformation runtime ships. The component keeps its data
		/// either way.
		/// </summary>
		public static bool RuntimeAvailable => false;

		#region Packaging

		public byte[] Pack() => RubberDeformationRendererPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> null;

		public void Unpack(byte[] bytes) => RubberDeformationRendererPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs,
			PackagedFiles files)
		{
		}

		#endregion
	}

	public struct RubberDeformationRendererPackable
	{
		private const int CurrentVersion = 1;

		public int Version;
		public float UpdateThreshold;
		public bool RecalculateNormals;
		public bool IncludeSupportedArcs;

		public static byte[] Pack(RubberDeformationRenderer component)
		{
			return PackageApi.Packer.Pack(new RubberDeformationRendererPackable {
				Version = CurrentVersion,
				UpdateThreshold = component.UpdateThreshold,
				RecalculateNormals = component.RecalculateNormals,
				IncludeSupportedArcs = component.IncludeSupportedArcs,
			});
		}

		public static void Unpack(byte[] bytes, RubberDeformationRenderer component)
		{
			if (bytes == null || bytes.Length == 0) {
				return;
			}
			var data = PackageApi.Packer.Unpack<RubberDeformationRendererPackable>(bytes);
			component.UpdateThreshold = data.UpdateThreshold;
			component.RecalculateNormals = data.RecalculateNormals;
			component.IncludeSupportedArcs = data.IncludeSupportedArcs;
		}
	}
}
