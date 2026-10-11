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

using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[PackAs("RubberMesh")]
	[ExecuteInEditMode]
	[AddComponentMenu("Pinball/Mesh/Rubber Mesh")]
	public class RubberMeshComponent : MeshComponent<RubberData, RubberComponent>, IPackable
	{
		/// <summary>
		/// Gives a new rubber a renderer with the default table material. Only the
		/// authoring that creates a rubber calls it, so a rubber whose renderer was removed
		/// on purpose stays collision-only.
		/// </summary>
		public bool TryEnsureRenderer(out MeshRenderer renderer, out string error)
		{
			renderer = GetComponent<MeshRenderer>();
			var created = !renderer;
			if (!renderer) {
				renderer = gameObject.AddComponent<MeshRenderer>();
			}
			error = null;
			if (!renderer.sharedMaterial) {
				var materialPath = RenderPipeline.Current.Type == RenderPipelineType.Standard
					? "Materials/Table Opaque (Builtin)"
					: "Materials/TableOpaque";
				var material = Resources.Load<UnityEngine.Material>(materialPath);
				if (!material) {
					error = $"The default material resource '{materialPath}' is missing.";
					return false;
				}
				renderer.sharedMaterial = material;
			}
			if (created) {
				renderer.enabled = true;
			}
			return true;
		}

		protected override Mesh GetMesh(RubberData data)
			=> new RubberMeshGenerator(MainComponent)
				.GetTransformedMesh(0, MainComponent.PlayfieldDetailLevel)
				.TransformToWorld();

		protected override PbrMaterial GetMaterial(RubberData data, Table table)
			=> new RubberMeshGenerator(MainComponent).GetMaterial(table, data);

		#region Packaging

		public byte[] Pack() => PackageApi.Packer.Empty;

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) { }

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion
	}
}
