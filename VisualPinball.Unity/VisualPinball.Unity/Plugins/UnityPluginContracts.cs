// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The Unity scene of the game being played: the table's root object and its gamelogic engine. Plugins that need to
	/// add objects to the table or talk to a specific engine get it from the game's capabilities.
	/// </summary>
	/// <remarks>
	/// The Player publishes one instance under <see cref="UnityPluginCapability.GameScene"/> for each game, before plugins
	/// receive <see cref="IPlayerPlugin.PrepareGameAsync"/>, and withdraws it when the game stops. The PinMAME plugin uses
	/// it to find the <c>PinMameGamelogicEngine</c> under the table, the DMD plugin parents its bridge object to
	/// <see cref="Table"/>, and the B2S plugin attaches its in-scene backglass to it. All access belongs on the owner
	/// (main) thread.
	/// <example>
	/// <code>
	/// public override Task PrepareGameAsync(PluginGameContext game, CancellationToken cancellation)
	/// {
	///     var scene = game.Capabilities.Require&lt;IUnityGameScene&gt;(UnityPluginCapability.GameScene);
	///     _root = new GameObject("My plugin");
	///     _root.transform.SetParent(scene.Table.transform, false);
	///     game.Lifetime.Own(new Subscription(() =&gt; { if (_root != null) UnityEngine.Object.Destroy(_root); }));
	///     return Task.CompletedTask;
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IUnityGameScene
	{
		/// <summary>
		/// Root object of the loaded table. Valid for the duration of the game; objects a plugin adds under it are destroyed
		/// with the table unless the plugin removes them first.
		/// </summary>
		GameObject Table { get; }
		/// <summary>
		/// The table's gamelogic engine, found on the same object as its <see cref="Player"/> component, or null when the
		/// table has no Player or no engine. Cast it to an optional interface (such as <see cref="IGameIdentityProvider"/>)
		/// or a concrete engine type to reach engine-specific features.
		/// </summary>
		IGamelogicEngine Gamelogic { get; }
	}
	/// <summary>
	/// Default immutable <see cref="IUnityGameScene"/>, created by the Player for each game it prepares.
	/// </summary>
	/// <remarks>
	/// Plugins consume <see cref="IUnityGameScene"/> and have no reason to create this type; a host or test that publishes
	/// <see cref="UnityPluginCapability.GameScene"/> itself can use it.
	/// <example>
	/// How the Player publishes the scene for the game's lifetime:
	/// <code>
	/// _gameScope.Own(Host.Capabilities.Publish&lt;IUnityGameScene&gt;(UnityPluginCapability.GameScene, new UnityGameScene(table, engine)));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class UnityGameScene : IUnityGameScene
	{
		/// <inheritdoc/>
		public GameObject Table { get; }
		/// <inheritdoc/>
		public IGamelogicEngine Gamelogic { get; }
		/// <summary>Creates a scene description. Neither argument is validated.</summary>
		/// <param name="table">Root object of the loaded table.</param>
		/// <param name="gamelogic">The table's gamelogic engine, or null when it has none.</param>
		public UnityGameScene(GameObject table, IGamelogicEngine gamelogic) { Table = table; Gamelogic = gamelogic; }
	}
	/// <summary>
	/// Implemented by a gamelogic engine to declare which display frame encodings it can produce, so VPE publishes its
	/// display resources with the right formats and negotiates raw frames correctly.
	/// </summary>
	/// <remarks>
	/// <see cref="GamelogicPluginResources"/> reads this when a game is prepared. The formats are advertised in each display's
	/// <see cref="PluginResourceDescriptor.Formats"/>. When a consumer binds a display for
	/// <see cref="ResourceView.Identification"/> (for example the DMD plugin matching colorization frames), VPE requests raw
	/// <see cref="DisplayEncoding.Gray2"/> or <see cref="DisplayEncoding.Gray4"/> frames through
	/// <see cref="IDisplayFrameFormatPreference"/> and returns to <see cref="DefaultDisplayFormat"/> when the last such
	/// consumer leaves. Native display negotiation may apply to the entire provider, even when the API accepts a display ID;
	/// <see cref="FormatAppliesToAllDisplays"/> declares that.
	/// <para>
	/// Without this interface VPE assumes <c>Gray2 | Gray4 | Luminance8 | Rgb24</c> for engines that implement
	/// <see cref="IDisplayFrameFormatPreference"/> and <c>Luminance8 | Rgb24</c> otherwise, with
	/// <see cref="DisplayEncoding.Luminance8"/> as default and per-display negotiation.
	/// </para>
	/// <example>
	/// PinMAME switches the frame format of all its displays at once:
	/// <code>
	/// DisplayEncoding IDisplayResourceMetadata.DisplayFormats =&gt; DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8;
	/// DisplayEncoding IDisplayResourceMetadata.DefaultDisplayFormat =&gt; DisplayEncoding.Luminance8;
	/// bool IDisplayResourceMetadata.FormatAppliesToAllDisplays =&gt; true;
	/// </code>
	/// </example>
	/// </remarks>
	public interface IDisplayResourceMetadata
	{
		/// <summary>All encodings the engine can deliver for its dot-matrix displays, as flags.</summary>
		DisplayEncoding DisplayFormats { get; }
		/// <summary>
		/// The single encoding the engine delivers when no consumer asks for raw frames. VPE requests it again when the last
		/// identification consumer leaves, or when the game's resources are disposed while raw frames were requested.
		/// </summary>
		DisplayEncoding DefaultDisplayFormat { get; }
		/// <summary>
		/// True when a format request changes every display of the engine, not just the one named. VPE then negotiates once
		/// for all displays and calls <see cref="IDisplayFrameFormatPreference.RequestDisplayFrameFormat"/> with an empty
		/// display ID. False negotiates each display separately.
		/// </summary>
		bool FormatAppliesToAllDisplays { get; }
	}
	/// <summary>
	/// Implemented by a gamelogic engine that produces its own sound (such as ROM audio), so VPE can publish it as a plugin
	/// audio resource.
	/// </summary>
	/// <remarks>
	/// When a game is prepared, <see cref="GamelogicPluginResources"/> publishes
	/// <see cref="IGamelogicAudioResourceProvider.AudioSource"/> as a <see cref="ResourceKind.Audio"/> resource with the
	/// role <c>rom-mix</c> and the ID <c>&lt;controller&gt;/audio/rom</c>, where plugins can bind it. PinMAME exposes its emulated sound board this way.
	/// <example>
	/// <code>
	/// private readonly AudioResourceSource _pluginAudio = new AudioResourceSource(error =&gt; Debug.LogException(error));
	/// IPluginAudioSource IGamelogicAudioResourceProvider.AudioSource =&gt; _pluginAudio;
	/// </code>
	/// </example>
	/// </remarks>
	public interface IGamelogicAudioResourceProvider
	{
		/// <summary>
		/// The engine's audio stream. Read once when the game's resources are created; must not be null. The engine owns the
		/// source and publishes frames to it.
		/// </summary>
		IPluginAudioSource AudioSource { get; }
	}

	/// <summary>
	/// Receives every controller output in the controller's own numbering, on Unity's main thread.
	/// </summary>
	/// <remarks>
	/// VPE implements this interface and hands it to engines through
	/// <see cref="IGamelogicNativeOutputs.SetNativeOutputListener"/>; engine authors call it, plugins do not implement it.
	/// VPE republishes each call as a <see cref="PluginControllerEvent"/> on the game's <see cref="ResourceKind.Outputs"/>
	/// resource, where the B2S backglass reads ROM lamps and solenoids by number. Calls made from another thread are posted
	/// to the owner thread.
	/// </remarks>
	public interface INativeOutputListener
	{
		/// <summary>Reports the new value of one output.</summary>
		/// <param name="kind">
		/// The output type: <see cref="ControllerEventKind.Lamp"/>, <see cref="ControllerEventKind.GeneralIllumination"/>,
		/// <see cref="ControllerEventKind.Coil"/> or <see cref="ControllerEventKind.Mech"/>.
		/// </param>
		/// <param name="number">The output's number as the controller defines it, such as a PinMAME lamp number.</param>
		/// <param name="value">The raw value, interpreted through <paramref name="semantics"/> and <paramref name="maximum"/>.</param>
		/// <param name="semantics">What <paramref name="value"/> means (on/off, levels, faded brightness or position).</param>
		/// <param name="maximum">The value that means fully on, or the top of the range; 1 for binary outputs.</param>
		void NativeOutputChanged(ControllerEventKind kind, int number, double value, ControllerValueSemantics semantics, double maximum);
		/// <summary>
		/// The controller finished one poll. VPE publishes it as <see cref="ControllerEventKind.PollEnd"/> so consumers can
		/// apply the outputs they collected in one batch. Call it after the poll's last <see cref="NativeOutputChanged"/>.
		/// </summary>
		void NativeOutputPollEnd();
	}

	/// <summary>
	/// A gamelogic engine that reports all lamps, solenoids, GI strings and mechs by native number, independent of the
	/// table's mapping. Backglass-only outputs (match, tilt, ball in play) are usually not mapped to anything on the
	/// playfield, so the mapped events alone miss them.
	/// </summary>
	/// <remarks>
	/// When an engine implements this interface, <see cref="GamelogicPluginResources"/> publishes an extra
	/// <see cref="ResourceKind.Outputs"/> resource (<c>&lt;controller&gt;/outputs</c>) and installs a listener for the
	/// game's duration. The B2S plugin prefers that resource over the mapped controller events when it exists.
	/// <example>
	/// PinMAME forwards each poll's changes after dispatching its mapped events:
	/// <code>
	/// public void SetNativeOutputListener(INativeOutputListener listener) =&gt; _nativeOutputs = listener;
	///
	/// private void DispatchNativeOutputs()
	/// {
	///     var listener = _nativeOutputs;
	///     if (listener == null) return;
	///     foreach (var lamp in _changedLamps) {
	///         listener.NativeOutputChanged(ControllerEventKind.Lamp, lamp.Id, lamp.Value, ControllerValueSemantics.Binary, 1);
	///     }
	///     listener.NativeOutputPollEnd();
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IGamelogicNativeOutputs
	{
		/// <summary>
		/// Sets the single listener, or null. Changes are delivered from the engine's main-thread poll. VPE sets a listener
		/// when the game's resources are created and passes null when they are disposed; the engine should then drop any
		/// queued outputs.
		/// </summary>
		/// <param name="listener">The listener to call for every output change, or null to stop reporting.</param>
		void SetNativeOutputListener(INativeOutputListener listener);
	}

	/// <summary>
	/// Implemented by a gamelogic engine to declare the layout and technology of its segment displays, as published in
	/// <see cref="PluginSegmentFrame"/>.
	/// </summary>
	/// <remarks>
	/// When an engine announces a segment display (a requested display with a height of 1), <see cref="GamelogicPluginResources"/>
	/// asks this interface for its layout and publishes a <see cref="ResourceKind.Segments"/> resource. Without an answer
	/// the layout is <c>seg16</c> with no hardware hint. The B2S backglass uses the layout and hint to draw its segment
	/// digits. PinMAME maps its display types onto the layouts and reports <c>gottlieb</c> for upright Gottlieb
	/// System 80 glass displays.
	/// </remarks>
	public interface ISegmentDisplayMetadata
	{
		/// <summary>
		/// Returns the layout (<c>seg7</c>, <c>seg9</c>, <c>seg14</c>, <c>seg16</c>) and hardware hint of a display.
		/// Called on the main thread when the display is announced, so the engine must know the layout by then.
		/// </summary>
		/// <param name="displayId">The display ID the engine used when it announced the display.</param>
		/// <param name="layout">The segment layout; see <see cref="PluginSegmentFrame.Layout"/>.</param>
		/// <param name="hardware">The technology hint, or an empty string; see <see cref="PluginSegmentFrame.Hardware"/>.</param>
		/// <returns>True when the engine knows the display; false to use the defaults.</returns>
		bool TryGetSegmentLayout(string displayId, out string layout, out string hardware);
	}
	/// <summary>
	/// Keys of the capabilities the Unity Player publishes to plugins through <see cref="PluginCapabilities"/>.
	/// </summary>
	/// <remarks>
	/// Plugins read them from <see cref="PluginGameContext.Capabilities"/> or <see cref="PluginApplicationContext.Capabilities"/>
	/// (the same object) with <see cref="PluginCapabilities.Require{T}"/>, which throws when the host does not provide the
	/// capability, or with <see cref="PluginCapabilities.Find{T}"/>, which returns null.
	/// <example>
	/// <code>
	/// var scene = game.Capabilities.Require&lt;IUnityGameScene&gt;(UnityPluginCapability.GameScene);
	/// var outputs = game.Capabilities.Require&lt;PluginOutputLeases&gt;(UnityPluginCapability.Outputs);
	/// </code>
	/// </example>
	/// </remarks>
	public static class UnityPluginCapability
	{
		/// <summary>
		/// The current game's <see cref="IUnityGameScene"/>. Published per game before plugins prepare it and removed when
		/// the game stops.
		/// </summary>
		public const string GameScene = "vpe.unity.game-scene.v1";
		/// <summary>
		/// The application-wide <see cref="PluginEvents"/> bus for typed notifications between plugins, delivered on the owner
		/// thread.
		/// </summary>
		public const string Events = "vpe.events.v1";
		/// <summary>
		/// The application-wide <see cref="PluginOutputLeases"/>, which gives one plugin exclusive use of a physical output
		/// device. The DMD plugin leases its ZeDMD, PinDMD3 and Pixelcade ports through it.
		/// </summary>
		public const string Outputs = "vpe.outputs.v1";
	}
}
