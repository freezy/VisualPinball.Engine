// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class StandardMaterialAdapter : IMaterialAdapter
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void SetOpaque(GameObject gameObject)
		{
			Logger.Info("Not implemented for {0}: {1}", gameObject.name, "SetOpaque");
		}
		public void SetDoubleSided(GameObject gameObject)
		{
			throw new System.NotImplementedException();
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
