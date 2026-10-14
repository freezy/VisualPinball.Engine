// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A <see cref="ResourceKind.Surface"/> resource backed by a borrowed Unity texture. Its producer owns it; consumers may
	/// sample it until <see cref="Changed"/> or resource removal.
	/// </summary>
	/// <remarks>
	/// Plugins that render an image other plugins can show publish it as a surface with this interface, so consumers in the
	/// same Unity process can use the texture directly on the GPU. PinUP publishes the render texture of each PUP screen
	/// that shows video (role <c>topper</c> for screen 0, <c>dmd</c> for screen 1, <c>backglass</c> for screen 2,
	/// <c>full-dmd</c> for screen 5, <c>pup-screen-N</c> otherwise), and the B2S plugin publishes its composed backglass
	/// with the role <c>backglass</c>; see <see cref="SurfaceRoles"/>. Both report <c>unity-render-texture</c> as
	/// <see cref="IPluginSurface.Backend"/>. <see cref="CabinetScreenBinder"/> shows these surfaces on the table's
	/// <see cref="CabinetScreenComponent"/>s. Consumers must not modify, resize or release the texture, and must not keep
	/// using it after the resource is removed.
	/// <example>
	/// Publishing a render texture:
	/// <code>
	/// private sealed class MySurface : IUnityTextureResource
	/// {
	///     private readonly RenderTexture _texture;
	///     public MySurface(RenderTexture texture) { _texture = texture; }
	///     public ResourceKind Kind =&gt; ResourceKind.Surface;
	///     public int Width =&gt; _texture.width;
	///     public int Height =&gt; _texture.height;
	///     public string Backend =&gt; "unity-render-texture";
	///     public Texture Texture =&gt; _texture;
	///     public event Action&lt;Texture&gt; Changed;
	/// }
	///
	/// game.Lifetime.Own(game.Resources.Publish(new PluginResourceDescriptor(game.ControllerId + "/surface/mine", _application.Plugin.Id,
	///     game.ControllerId, "My surface", SurfaceRoles.Backglass, ResourceKind.Surface, game.Session, 1), new MySurface(texture)));
	/// </code>
	/// </example>
	/// </remarks>
	public interface IUnityTextureResource : IPluginSurface
	{
		/// <summary>
		/// The current texture, or null while the producer has not created it yet. Read on the main thread. Keep using a
		/// texture only until <see cref="Changed"/> announces another one.
		/// </summary>
		Texture Texture { get; }
		/// <summary>
		/// Raised on the main thread when the producer creates, replaces (for example after a resize) or releases its texture.
		/// The argument is the new texture, or null when there is none any more.
		/// </summary>
		event Action<Texture> Changed;
	}
}
