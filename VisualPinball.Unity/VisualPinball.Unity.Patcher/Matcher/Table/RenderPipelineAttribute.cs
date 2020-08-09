namespace VisualPinball.Unity.Patcher.Matcher.Table
{
	/// <summary>
	/// Matches if the render pipeline is set to a given type.
	/// </summary>
	public class RenderPipelineAttribute : TableMatchAttribute
	{
		private readonly RenderPipelineType _type;

		public RenderPipelineAttribute(RenderPipelineType type)
		{
			_type = type;
		}

		public override bool Matches(Engine.VPT.Table.Table table, string fileName)
		{
			return RenderPipeline.Current == _type;
		}
	}
}
