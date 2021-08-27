using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Playfield
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Playfield Mesh")]
	public class PlayfieldMeshAuthoring : ItemMeshAuthoring<TableData, PlayfieldAuthoring>
	{
		#region Data

		public bool AutoGenerate = true;

		#endregion

		public static readonly Type[] ValidParentTypes = Type.EmptyTypes;

		public override IEnumerable<Type> ValidParents => ValidParentTypes;

		protected override RenderObject GetRenderObject(TableData data)
			=> new TableMeshGenerator(data).GetRenderObject(table, false);
	}
}
