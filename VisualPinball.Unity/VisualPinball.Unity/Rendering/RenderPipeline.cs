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

using System;
using System.Linq;
using NLog;

namespace VisualPinball.Unity
{
	public interface IRenderPipeline
	{
		string Name { get; }

		RenderPipelineType Type { get; }
		IMaterialConverter MaterialConverter { get; }
		ILightConverter LightConverter { get; }
	}

	public enum RenderPipelineType
	{
		Standard,
		Urp,
		Hdrp,
	}

	public static class RenderPipeline
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		public static IRenderPipeline Current {
			get {
				if (_current == null) {
					var t = typeof(IRenderPipeline);
					var pipelines = AppDomain.CurrentDomain.GetAssemblies()
						.Where(x => x.FullName.StartsWith("VisualPinball."))
						.SelectMany(x => x.GetTypes())
						.Where(x => x.IsClass && t.IsAssignableFrom(x))
						.Select(x => (IRenderPipeline) Activator.CreateInstance(x))
						.ToArray();

					_current = pipelines.Length == 1
						? pipelines.First()
						: pipelines.First(p => p.Type != RenderPipelineType.Standard);
				}
				return _current;
			}
		}

		private static IRenderPipeline _current;
	}
}
