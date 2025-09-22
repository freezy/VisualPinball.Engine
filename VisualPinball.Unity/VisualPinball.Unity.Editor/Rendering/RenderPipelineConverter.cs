// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A common interface for all render pipelines that covers material
	/// creation, lighting setup and ball creation.
	/// </summary>
	public interface IRenderPipelineConverter
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
		/// Provides a bunch of helper methods for setting common attributes
		/// in materials.
		/// </summary>
		IMaterialAdapter MaterialAdapter { get; }

		/// <summary>
		/// Provides access to VPE's game item prefabs.
		/// </summary>
		IPrefabProvider PrefabProvider { get; }
	}

	/// <summary>
	/// A global static class that checks which render pipeline implementations
	/// are available and instantiates an SRP if available or the included
	/// built-in instance otherwise.
	/// </summary>
	public static class RenderPipelineConverter
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static IRenderPipelineConverter _current;

		/// <summary>
		/// Returns the currently instantiated render pipeline.
		/// </summary>
		public static IRenderPipelineConverter Current {
			get {
				if (_current == null) {
					Debug.Log("Detecting render pipeline converter...");
					var t = typeof(IRenderPipelineConverter);
					var pipelines = AppDomain.CurrentDomain.GetAssemblies()
						.Where(x => x.FullName.StartsWith("VisualPinball."))
						.SelectMany(x => x.GetTypes())
						.Where(x => x.IsClass && t.IsAssignableFrom(x))
						.Select(x => (IRenderPipelineConverter) Activator.CreateInstance(x))
						.ToArray();

					Debug.Log("Found pipelines: " + string.Join(", ", pipelines.Select(p => p.Name)));

					_current = pipelines.Length == 1
						? pipelines.First()
						: pipelines.First(p => p.Type != RenderPipelineType.Standard);

					Debug.Log($"Instantiated {_current.Name}.");
					Logger.Info($"Instantiated {_current.Name}.");
				}
				return _current;
			}
		}
	}
}
