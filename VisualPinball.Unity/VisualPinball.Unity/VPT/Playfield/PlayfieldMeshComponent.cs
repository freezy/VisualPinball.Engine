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
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity.Playfield
{
	[PackAs("PlayfieldMesh")]
	[ExecuteInEditMode]
	[AddComponentMenu("Pinball/Mesh/Playfield Mesh")]
	public class PlayfieldMeshComponent : MeshComponent<TableData, PlayfieldComponent>, IPackable, IColliderMesh
	{
		#region Data

		public bool AutoGenerate = true;

		#endregion

		#region Packaging

		// Visible custom meshes travel in table.glb. Collision-only meshes need colliders.glb because
		// the scene exporter omits meshes without an enabled renderer.
		public byte[] Pack() => PlayfieldMeshPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => PlayfieldMeshReferencesPackable.Pack(this, files);

		public void Unpack(byte[] bytes) => PlayfieldMeshPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) => PlayfieldMeshReferencesPackable.Unpack(data, this, files);

		public int NumColliderMeshes {
			get {
				if (AutoGenerate || !TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh) return 0;
				var renderer = GetComponent<MeshRenderer>();
				return !(renderer && renderer.enabled) ? 1 : 0;
			}
		}
		public UnityEngine.Mesh GetColliderMesh(int index) => index == 0 && TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;

		#endregion

		// A manually assigned playfield mesh may contain authored openings for scoops, troughs, and
		// other devices. Treating it as procedural lets MeshComponent.Awake regenerate the solid table
		// plane after a domain reload and silently replace those openings.
		protected override bool IsProcedural => AutoGenerate;

		protected override Mesh GetMesh(TableData data)
			=> new TableMeshGenerator(data).GetMesh().TransformToWorld();

		protected override PbrMaterial GetMaterial(TableData data, Table table)
			=> new TableMeshGenerator(data).GetMaterial(table);
	}
}
