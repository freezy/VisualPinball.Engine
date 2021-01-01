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

using System;
using System.Linq;
using NLog;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A common interface for all render pipelines that covers material
	/// creation, lighting setup and ball creation.
	/// </summary>
	public interface IRenderPipeline
	{
		/// <summary>
		/// Name of the render pipeline
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Type of the render pipeline.
		/// </summary>
		RenderPipelineType Type { get; }


		/// <summary>
		/// Converts a material from Visual Pinball to the active renderer.
		/// </summary>
		IMaterialConverter MaterialConverter { get; }

		/// <summary>
		/// Provides a bunch of helper methods for setting common attributes
		/// in materials.
		/// </summary>
		IMaterialAdapter MaterialAdapter { get; }

		/// <summary>
		/// Converts a light from Visual Pinball to the active renderer.
		/// </summary>
		ILightConverter LightConverter { get; }

		/// <summary>
		/// Creates a new ball.
		/// </summary>
		IBallConverter BallConverter { get; }
	}

	public enum RenderPipelineType
	{
		/// <summary>
		/// The built-in renderer.
		/// </summary>
		Standard,

		/// <summary>
		/// The Universal Render Pipeline.
		/// </summary>
		Urp,

		/// <summary>
		/// The High Definition Render Pipeline.
		/// </summary>
		Hdrp,
	}

	/// <summary>
	/// A global static class that checks which render pipeline implementations
	/// are available and instantiates an SRP if available or the included
	/// built-in instance otherwise.
	/// </summary>
	public static class RenderPipeline
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static IRenderPipeline _current;

		/// <summary>
		/// Returns the currently instantiated render pipeline.
		/// </summary>
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

					Logger.Info($"Instantiated ${_current.Name}.");
				}
				return _current;
			}
		}
	}
}
