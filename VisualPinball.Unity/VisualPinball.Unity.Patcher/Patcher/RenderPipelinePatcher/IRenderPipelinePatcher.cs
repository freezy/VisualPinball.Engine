using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VisualPinball.Unity.Patcher.RenderPipelinePatcher
{
	public interface IRenderPipelinePatcher
	{
		/// <summary>
		/// Set the material of the gameobject to opaque.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetOpaque(GameObject gameObject);

		/// <summary>
		/// Set the material of the gameobject to double sided.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetDoubleSided(GameObject gameObject);

		void SetTransparentDepthPrepassEnabled(GameObject gameObject);

		/// <summary>
		/// Set the AlphaCutOff value for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetAlphaCutOff(GameObject gameObject, float value);

		/// <summary>
		/// Enable AlphaCutOff for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetAlphaCutOffEnabled(GameObject gameObject);

		/// <summary>
		/// Disable NormalMap for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetNormalMapDisabled(GameObject gameObject);

		/// <summary>
		/// Set the Metallic value for the material of the gameobject.
		/// </summary>
		/// <param name="gameObject"></param>
		void SetMetallic(GameObject gameObject, float value);
	}
}
