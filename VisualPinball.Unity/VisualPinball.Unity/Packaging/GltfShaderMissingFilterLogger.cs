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
using GLTFast.Logging;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Wraps a glTFast logger and drops the expected "shader missing" warnings for the glTF
	/// shadergraphs that VPE intentionally keeps out of the build (their ray-tracing passes don't
	/// compile). The HDRP material resolver replaces every imported glTF material at runtime, so
	/// gltFast's fallback shadergraph is never actually used and its absence is not an error.
	/// Every other log message passes through unchanged.
	/// </summary>
	internal sealed class GltfShaderMissingFilterLogger : ICodeLogger
	{
		private readonly ICodeLogger _inner;

		public GltfShaderMissingFilterLogger(ICodeLogger inner)
		{
			_inner = inner;
		}

		public void Error(LogCode code, params string[] messages)
		{
			if (code == LogCode.ShaderMissing && IsStrippedGltfShader(messages)) {
				return;
			}
			_inner.Error(code, messages);
		}

		public void Warning(LogCode code, params string[] messages) => _inner.Warning(code, messages);

		public void Info(LogCode code, params string[] messages) => _inner.Info(code, messages);

		// Explicitly implemented (rather than relying on the interface default) so the filtering in
		// Error() also applies when callers route through Log().
		public void Log(LogType logType, LogCode code, params string[] messages)
		{
			switch (logType) {
				case LogType.Log:
					Info(code, messages);
					break;
				case LogType.Warning:
					Warning(code, messages);
					break;
				default:
					Error(code, messages);
					break;
			}
		}

		public void Error(string message) => _inner.Error(message);

		public void Warning(string message) => _inner.Warning(message);

		public void Info(string message) => _inner.Info(message);

		private static bool IsStrippedGltfShader(string[] messages)
		{
			if (messages == null) {
				return false;
			}
			foreach (var message in messages) {
				if (message != null && message.IndexOf("glTF-pbr", StringComparison.Ordinal) >= 0) {
					return true;
				}
			}
			return false;
		}
	}
}
