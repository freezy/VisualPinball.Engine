namespace VisualPinball.Unity.Patcher.Matcher
{
	public enum RenderPipelineType
	{
		BuiltIn,
		Hdrp,
		Urp
	}

	public static class RenderPipeline
	{
		public static RenderPipelineType Current
		{
			get { if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null) {

					if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("UniversalRenderPipelineAsset")) {
						return RenderPipelineType.Urp;
					}

					if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("HDRenderPipelineAsset")) {
						return RenderPipelineType.Hdrp;
					}
				}
				return RenderPipelineType.BuiltIn; }
		}
	}
}
