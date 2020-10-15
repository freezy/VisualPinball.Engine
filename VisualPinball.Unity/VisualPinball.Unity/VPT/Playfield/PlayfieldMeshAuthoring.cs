using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Playfield
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Playfield Mesh")]
	public class PlayfieldMeshAuthoring : ItemMeshAuthoring<Table, TableData, TableAuthoring>
	{
		protected override bool IsVisible {
			get => true;
			set { }
		}
	}
}
