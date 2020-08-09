using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.Patcher.Matcher.Item
{
	/// <summary>
	/// Matches if the render pipeline is set to a given type.
	/// </summary>
	public class RenderPipelineAttribute : ItemMatchAttribute
	{
		private readonly RenderPipelineType _type;

		public RenderPipelineAttribute(RenderPipelineType type)
		{
			_type = type;
		}

		public override bool Matches(Engine.VPT.Table.Table table, IRenderable item, RenderObject ro, GameObject obj)
		{
			return RenderPipeline.Current == _type;
		}
	}
}
