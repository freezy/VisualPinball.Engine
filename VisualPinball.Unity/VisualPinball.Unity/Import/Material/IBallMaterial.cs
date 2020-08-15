using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Common interface for ball material for the various render pipelines
	/// </summary>
	public interface IBallMaterial
	{
		/// <summary>
		/// Get the shader for the currently detected graphics pipeline.
		/// </summary>
		/// <returns></returns>
		Shader GetShader();

		/// <summary>
		/// Create a ball material for the currently detected graphics pipeline.
		/// </summary>
		/// <param name="vpxMaterial"></param>
		/// <param name="table"></param>
		/// <param name="debug"></param>
		/// <returns></returns>
		Material CreateMaterial();
	}
}
