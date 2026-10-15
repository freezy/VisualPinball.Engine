// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System;
using NLog;
using VisualPinball.Plugins;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A plugin's log, written to NLog under the logger "Plugin.&lt;plugin id&gt;".
	/// </summary>
	public sealed class NLogPluginLog : IPluginLog
	{
		private readonly Logger _logger;

		public NLogPluginLog(PluginDescriptor plugin)
		{
			_logger = LogManager.GetLogger("Plugin." + plugin.Id);
		}

		public bool IsEnabled(PluginLogLevel level) => _logger.IsEnabled(Map(level));

		public void Write(PluginLogLevel level, string message, Exception exception = null)
		{
			// plugin messages are text, not templates
			_logger.Log(new LogEventInfo(Map(level), _logger.Name, null, message, null, exception));
		}

		private static LogLevel Map(PluginLogLevel level)
		{
			switch (level) {
				case PluginLogLevel.Debug: return LogLevel.Debug;
				case PluginLogLevel.Warn: return LogLevel.Warn;
				case PluginLogLevel.Error: return LogLevel.Error;
				default: return LogLevel.Info;
			}
		}
	}
}
