using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Playfield
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Playfield Mesh")]
	public class PlayfieldMeshAuthoring : ItemMeshAuthoring<Table, TableData, PlayfieldAuthoring>
	{
		#region Data

		public bool IsSimple;

		#endregion

		public static readonly Type[] ValidParentTypes = Type.EmptyTypes;

		public override IEnumerable<Type> ValidParents => ValidParentTypes;

		protected override RenderObject GetRenderObject(TableData data, Table table)
			=> new TableMeshGenerator(data).GetRenderObject(table, false);
	}
}
