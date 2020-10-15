using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Patcher.RenderPipelinePatcher
{
	public class UrpPatcher : IRenderPipelinePatcher
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void SetOpaque(GameObject gameObject)
		{
			Logger.Info("Not implemented for {0}: {1}", gameObject.name, "SetOpaque");
		}

		public void SetDoubleSided(GameObject gameObject)
		{
			Logger.Info("Not implemented for {0}: {1}", gameObject.name, "SetDoubleSided");
		}

		public void SetTransparentDepthPrepassEnabled(GameObject gameObject)
		{
			Logger.Info("Not implemented for {0}: {1}", gameObject.name, "SetTransparentDepthPrepassEnabled");
		}


		public void SetAlphaCutOff(GameObject gameObject, float value)
		{
			Logger.Info("Not implemented for {0}: {1}", gameObject.name, "SetAlphaCutOff");
		}

		public void SetAlphaCutOffEnabled(GameObject gameObject)
		{
			Logger.Info("Not implemented for {0}: {1}", gameObject.name, "SetAlphaCutOffEnabled");
		}

		public void SetNormalMapDisabled(GameObject gameObject)
		{
			Logger.Info("Not implemented for {0}: {1}", gameObject.name, "SetNormalMapDisabled");
		}

		public void SetMetallic(GameObject gameObject, float value)
		{
			Logger.Info("Not implemented for {0}: {1}", gameObject.name, "SetMetallic");
		}
	}
}
