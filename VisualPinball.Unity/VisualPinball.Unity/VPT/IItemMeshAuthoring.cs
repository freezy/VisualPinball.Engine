using System.Collections.Generic;
using System.Reflection;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public interface IItemMeshAuthoring : IItemAuthoring
	{
		bool MeshDirty { get; set; }
		ItemData ItemData { get; }

		List<MemberInfo> MaterialRefs { get; }
		List<MemberInfo> TextureRefs { get; }

		void RebuildMeshes();
	}
}
