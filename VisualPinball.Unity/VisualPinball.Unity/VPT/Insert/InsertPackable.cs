// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>Runtime insert state. Its finished mesh and UVs come from table.glb.</summary>
	public struct InsertPackable
	{
		public bool UsePlayfieldNormals;

		public static byte[] Pack(InsertComponent insert) => PackageApi.Packer.Pack(new InsertPackable {
			UsePlayfieldNormals = insert.UsePlayfieldNormals,
		});

		public static void Unpack(byte[] bytes, InsertComponent insert)
		{
			var data = PackageApi.Packer.Unpack<InsertPackable>(bytes);
			insert.RestoreCoverState(data.UsePlayfieldNormals, null);
			insert.UseImportedMesh(true);
		}

		public static byte[] PackReferences(InsertComponent insert, PackagedRefs refs)
			=> PackageApi.Packer.Pack(new InsertReferencesPackable {
				PlayfieldNodeId = refs.GetNodeId(insert.CoverPlayfield ? insert.CoverPlayfield.transform : null),
			});

		public static void UnpackReferences(byte[] bytes, InsertComponent insert, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<InsertReferencesPackable>(bytes);
			var playfield = refs.GetNode(data.PlayfieldNodeId);
			insert.RestoreCoverState(insert.UsePlayfieldNormals,
				playfield ? playfield.GetComponent<MeshRenderer>() : null);
		}
	}

	public struct InsertReferencesPackable
	{
		public string PlayfieldNodeId;
	}
}
