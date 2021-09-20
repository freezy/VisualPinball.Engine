using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity.Playfield
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Playfield Mesh")]
	public class PlayfieldMeshComponent : MeshComponent<TableData, PlayfieldComponent>
	{
		#region Data

		public bool AutoGenerate = true;

		#endregion

		public static readonly Type[] ValidParentTypes = Type.EmptyTypes;

		public override IEnumerable<Type> ValidParents => ValidParentTypes;

		protected override Mesh GetMesh(TableData data)
			=> new TableMeshGenerator(data).GetMesh();

		protected override PbrMaterial GetMaterial(TableData data, Table table)
			=> new TableMeshGenerator(data).GetMaterial(table);
	}
}
