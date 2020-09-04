// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using NLog.Layouts;
using NLog.Targets;
using UnityEngine;
using ILogger = UnityEngine.ILogger;

namespace VisualPinball.Unity
{
	[Target("Unity")]
	internal class UnityTarget : TargetWithLayout
	{
		private static readonly ILogger Logger = Debug.unityLogger;
		private const string Tag = "VisualPinball";

		public UnityTarget()
		{
			Layout = (Layout) "${logger}|${message}";
		}

		protected override void Write(LogEventInfo logEvent)
		{
			var msg = Layout.Render(logEvent);
			if (logEvent.Level.Ordinal >= 4) {
				Logger.LogError(Tag, msg);

			} else if (logEvent.Level.Ordinal >= 3) {
				Logger.LogWarning(Tag, msg);

			} else {
				Logger.Log(Tag, msg);
			}

			if (logEvent.Exception != null) {
				Logger.LogException(logEvent.Exception);
			}
		}
	}
}
