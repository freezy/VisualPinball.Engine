using System.Text;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Import.Material
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
		UnityEngine.Material CreateMaterial(PbrMaterial vpxMaterial, TableBehavior table, StringBuilder debug = null);

	}
}
