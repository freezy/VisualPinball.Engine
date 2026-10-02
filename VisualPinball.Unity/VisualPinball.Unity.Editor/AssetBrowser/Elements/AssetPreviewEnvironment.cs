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
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// What a render pipeline adds to a <see cref="PreviewRenderUtility"/> so
	/// that an asset previews as it looks on a table: Unity's own preview
	/// renders a floating mesh under a fixed exposure of zero, with the screen
	/// space effects off, which blows VPE's physically bright emission out to
	/// white and turns a refracting insert cover into an opaque disc.
	/// </summary>
	public interface IAssetPreviewEnvironment : IDisposable
	{
		/// <summary>Called right before the utility renders a frame.</summary>
		void BeginRender();

		/// <summary>Called right after the utility rendered a frame.</summary>
		void EndRender();
	}

	public interface IAssetPreviewEnvironmentProvider
	{
		/// <summary>
		/// Sets the utility's camera and lights up for the pipeline and adds
		/// whatever else the preview scene needs. The environment lives as long
		/// as the utility does.
		/// </summary>
		IAssetPreviewEnvironment Create(PreviewRenderUtility utility);
	}

	/// <summary>
	/// The pipeline's preview environment, registered by the pipeline package
	/// on load. Without one, previews fall back to Unity's.
	/// </summary>
	public static class AssetPreviewEnvironmentProvider
	{
		private static IAssetPreviewEnvironmentProvider _active;

		public static bool IsAvailable => _active != null;

		public static void Register(IAssetPreviewEnvironmentProvider provider)
		{
			_active = provider;
		}

		public static IAssetPreviewEnvironment Create(PreviewRenderUtility utility)
		{
			return _active?.Create(utility);
		}
	}
}
