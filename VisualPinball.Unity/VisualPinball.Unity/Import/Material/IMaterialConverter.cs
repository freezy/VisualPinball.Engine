using System.Text;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Common interface for material conversion with the various render pipelines
	/// </summary>
	public interface IMaterialConverter
	{
		/// <summary>
		/// Get the shader for the currently detected graphics pipeline.
		/// </summary>
		/// <returns></returns>
		Shader GetShader();

		/// <summary>
		/// Create a material for the currently detected graphics pipeline.
		/// </summary>
		/// <param name="vpxMaterial"></param>
		/// <param name="table"></param>
		/// <param name="debug"></param>
		/// <returns></returns>
		UnityEngine.Material CreateMaterial(PbrMaterial vpxMaterial, TableAuthoring table, StringBuilder debug = null);

	}
}
