using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Playfield
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Playfield Mesh")]
	public class PlayfieldMeshAuthoring : ItemMeshAuthoring<Table, TableData, TableAuthoring>
	{
		public static readonly Type[] ValidParentTypes = new Type[0];

		public override IEnumerable<Type> ValidParents => ValidParentTypes;

		protected override bool IsVisible {
			get => true;
			set { }
		}
	}
}
