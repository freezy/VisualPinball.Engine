// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// The type of data a resource carries. It decides which source interface the published object must implement and
	/// is the first thing a <see cref="PluginResourceSelection"/> filters on.
	/// </summary>
	/// <remarks>
	/// <see cref="PluginResourceRegistry.Publish{T}"/> rejects a source that does not implement the interface of its kind.
	/// VPE publishes the game's controller, outputs, displays, segment displays and ROM audio when a game is prepared;
	/// plugins publish surfaces (PinUP screens, the B2S backglass) and derived displays (the DMD plugin's colorized view).
	/// <para>
	/// <see cref="Outputs"/> carries every controller output by its native number through <see cref="IPluginControllerSource"/>.
	/// It is separate from <see cref="Controller"/>, the outputs as the table maps them, so a controller binding without a
	/// role stays unambiguous.
	/// </para>
	/// </remarks>
	public enum ResourceKind
	{
		/// <summary>
		/// The game controller's switch, coil, lamp and GI events as the table maps them, addressed by the engine's IDs.
		/// The source implements <see cref="IPluginControllerSource"/>. VPE publishes one per game with the controller's ID
		/// and the role <c>controller</c>; PinUP turns its events into PUP triggers.
		/// </summary>
		Controller,
		/// <summary>
		/// A dot-matrix or video display delivering <see cref="PluginDisplayFrame"/>s through <see cref="IPluginDisplaySource"/>.
		/// VPE publishes each DMD as <c>&lt;controller&gt;/display/&lt;id&gt;</c> with the role <c>dmd</c>.
		/// </summary>
		Display,
		/// <summary>
		/// A segment (alphanumeric or numeric) display delivering <see cref="PluginSegmentFrame"/>s through
		/// <see cref="IPluginSegmentSource"/>. VPE publishes each one as <c>&lt;controller&gt;/segments/&lt;id&gt;</c> with
		/// the role <c>segments</c>; the B2S backglass draws them as its score digits.
		/// </summary>
		Segments,
		/// <summary>
		/// A PCM audio stream delivering <see cref="PluginPcmFrame"/>s through <see cref="IPluginAudioSource"/>. VPE publishes
		/// an engine's own sound (PinMAME's ROM audio) as <c>&lt;controller&gt;/audio/rom</c> with the role <c>rom-mix</c>.
		/// </summary>
		Audio,
		/// <summary>
		/// Player input actions delivering <see cref="PluginInputEvent"/>s through <see cref="IPluginInputSource"/>. Nothing
		/// in VPE publishes this kind yet.
		/// </summary>
		Input,
		/// <summary>
		/// An image other plugins can show, described by <see cref="IPluginSurface"/>. PinUP publishes each PUP screen and
		/// the B2S plugin its composed backglass as a surface.
		/// </summary>
		Surface,
		/// <summary>
		/// Named trigger events delivered as <see cref="PluginTriggerEvent"/>s through <see cref="IPluginTriggerSource"/>.
		/// Nothing in VPE publishes this kind yet.
		/// </summary>
		Triggers,
		/// <summary>
		/// Every controller output by its native number, including outputs the table does not map, delivered through
		/// <see cref="IPluginControllerSource"/>. VPE publishes it as <c>&lt;controller&gt;/outputs</c> with the role
		/// <c>outputs</c> for engines that report native outputs, such as PinMAME; the B2S backglass prefers it over
		/// <see cref="Controller"/> to drive ROM lamps and solenoids.
		/// </summary>
		Outputs
	}

	/// <summary>
	/// Which version of a resource's content is meant: what the controller produced, frames suited to recognizing that
	/// content, or a version processed for showing.
	/// </summary>
	/// <remarks>
	/// A <see cref="PluginResourceDescriptor"/> declares its view in <see cref="PluginResourceDescriptor.View"/>, and a
	/// <see cref="PluginResourceSelection"/> states the view it wants in <see cref="PluginResourceSelection.Intent"/>. The
	/// difference matters for displays: PinUP and the DMD plugin's colorization need the controller's raw shades
	/// (<see cref="Identification"/>), while the B2S live DMD wants the colorized frames the DMD plugin publishes
	/// (<see cref="Presentation"/>), or the original when there are none.
	/// </remarks>
	public enum ResourceView
	{
		/// <summary>
		/// As a descriptor view: the unmodified output of its producer, in whatever encoding the producer currently
		/// delivers. As an intent: match only original resources that are not derived from another resource. This is the
		/// default for both.
		/// </summary>
		Original,
		/// <summary>
		/// As an intent: match original, non-derived resources and, for displays, request unmodified
		/// <see cref="DisplayEncoding.Gray2"/> or <see cref="DisplayEncoding.Gray4"/> frames through
		/// <see cref="IPluginDisplaySource.AcquireView"/> for as long as the binding holds the source. Used to identify
		/// content, such as PinUP triggers on DMD frames or the DMD plugin's colorization matching. As a descriptor view it
		/// marks a resource whose frames are meant for identification; no VPE producer publishes that view yet.
		/// </summary>
		Identification,
		/// <summary>
		/// As a descriptor view: frames processed for showing, typically derived from an original resource listed in
		/// <see cref="PluginResourceDescriptor.Parents"/>, such as the DMD plugin's colorized RGB view of a ROM's DMD. As an
		/// intent: match presentation resources, and original ones too when
		/// <see cref="PluginResourceSelection.AllowOriginalFallback"/> is set.
		/// </summary>
		Presentation
	}

	/// <summary>The state of a <see cref="PluginResourceBinding{T}"/>, readable through its <c>State</c> property.</summary>
	public enum ResourceBindingState
	{
		/// <summary>
		/// No published resource matches the selection yet. The binding attaches automatically when one is published.
		/// </summary>
		Waiting,
		/// <summary>Exactly one resource matches and the binding's receivers are connected to it.</summary>
		Bound,
		/// <summary>
		/// The binding cannot attach: either several resources match and the selection must name one (for example with
		/// <see cref="PluginResourceSelection.ResourceId"/>), or the source refused the requested view. The binding's
		/// <c>Diagnostic</c> says which.
		/// </summary>
		Blocked,
		/// <summary>The binding was disposed and no longer follows the registry.</summary>
		Disposed
	}

	/// <summary>What happened to a resource, as reported to <see cref="PluginResourceRegistry.Observe"/> callbacks.</summary>
	public enum ResourceChangeKind
	{
		/// <summary>The resource was published.</summary>
		Added,
		/// <summary>
		/// The resource is still published but changed in a way that may let a refused consumer bind now, such as a display
		/// giving up the raw encoding another consumer could not accept. Raised by
		/// <see cref="PluginResourceRegistry.NotifyChanged"/>. A resource that is replaced is reported as
		/// <see cref="Removed"/> followed by <see cref="Added"/> instead.
		/// </summary>
		Changed,
		/// <summary>The resource's publication was disposed.</summary>
		Removed
	}

	/// <summary>
	/// Pixel encodings of display frames. As flags it lists the encodings a source can deliver or a consumer accepts; a
	/// single <see cref="PluginDisplayFrame"/> always carries exactly one of them.
	/// </summary>
	/// <remarks>
	/// Producers advertise their encodings in <see cref="PluginResourceDescriptor.Formats"/>, and consumers state theirs in
	/// <see cref="PluginResourceSelection.Accepts"/>. PinMAME, for example, advertises
	/// <c>Gray2 | Gray4 | Luminance8</c>, delivers <see cref="Luminance8"/> by default and switches to raw
	/// <see cref="Gray2"/> or <see cref="Gray4"/> while an identification consumer such as PinUP is bound.
	/// </remarks>
	[Flags]
	public enum DisplayEncoding
	{
		/// <summary>No encoding. Used for "nothing advertised" and "nothing negotiated"; never valid for a frame.</summary>
		None = 0,
		/// <summary>One byte per pixel holding a raw shade from 0 to 3, as a 2-bit DMD controller produces it.</summary>
		Gray2 = 1,
		/// <summary>One byte per pixel holding a raw shade from 0 to 15, as a 4-bit DMD controller produces it.</summary>
		Gray4 = 2,
		/// <summary>One byte per pixel holding a brightness from 0 to 255.</summary>
		Luminance8 = 4,
		/// <summary>Three bytes per pixel, in red, green, blue order, each from 0 to 255.</summary>
		Rgb24 = 8,
		/// <summary>
		/// Four bytes per pixel, in red, green, blue, alpha order, each from 0 to 255. How alpha applies is given by
		/// <see cref="PluginDisplayFrame.Alpha"/>.
		/// </summary>
		Rgba32 = 16
	}

	/// <summary>The color space of a display frame's color values.</summary>
	public enum FrameColorSpace
	{
		/// <summary>Values are linear light intensities.</summary>
		Linear,
		/// <summary>Values are sRGB-encoded. This is the default of <see cref="PluginDisplayFrame"/>.</summary>
		Srgb
	}

	/// <summary>How the alpha channel of a <see cref="DisplayEncoding.Rgba32"/> frame is to be interpreted.</summary>
	public enum FrameAlpha
	{
		/// <summary>The frame has no meaningful alpha; treat it as opaque. This is the default.</summary>
		None,
		/// <summary>Color values are not multiplied by alpha.</summary>
		Straight,
		/// <summary>Color values are already multiplied by alpha.</summary>
		Premultiplied
	}

	/// <summary>The sample format of a <see cref="PluginPcmFrame"/>.</summary>
	public enum PcmEncoding
	{
		/// <summary>Signed 16-bit integer samples, little endian, two bytes each.</summary>
		Int16LittleEndian,
		/// <summary>32-bit IEEE float samples, nominally from -1 to 1, four bytes each. PinMAME's ROM audio uses this.</summary>
		Float32
	}

	/// <summary>What a <see cref="PluginControllerEvent"/> reports.</summary>
	public enum ControllerEventKind
	{
		/// <summary>A switch changed; the value is 1 when closed and 0 when open.</summary>
		Switch,
		/// <summary>A coil (solenoid) changed.</summary>
		Coil,
		/// <summary>A lamp changed.</summary>
		Lamp,
		/// <summary>A general illumination string changed.</summary>
		GeneralIllumination,
		/// <summary>Reserved for controller triggers; VPE does not emit it yet.</summary>
		Trigger,
		/// <summary>Reserved for controller state values; VPE does not emit it yet.</summary>
		State,
		/// <summary>Raw position of an emulated mech, as the controller reports it.</summary>
		Mech,
		/// <summary>Ends one controller poll; consumers that batch per poll apply what they collected.</summary>
		PollEnd
	}
	/// <summary>What a controller event's raw value means.</summary>
	/// <remarks>
	/// One controller can mix encodings within one output kind, so each event of an <see cref="ResourceKind.Outputs"/>
	/// resource carries its own semantics. PinMAME, for instance, reports physically faded lamps on some hardware
	/// generations, binary lamps on others, and GI as levels from 0 to 8 on WPC.
	/// </remarks>
	public enum ControllerValueSemantics
	{
		/// <summary>0 or 1.</summary>
		Binary,
		/// <summary>Discrete levels from 0 to the event's maximum, such as WPC GI 0..8.</summary>
		Levels,
		/// <summary>Already faded physical brightness from 0 to the event's maximum.</summary>
		Physical,
		/// <summary>A position, such as a mech step; not a brightness.</summary>
		Position
	}

	/// <summary>
	/// Describes one published resource: its stable ID, who published it, which game controller it belongs to, what it is
	/// for and which version of the content it carries.
	/// </summary>
	/// <remarks>
	/// A producer creates a descriptor and passes it with its source to <see cref="PluginResourceRegistry.Publish{T}"/>;
	/// consumers see descriptors in <see cref="PluginResourceRegistry.Snapshot()"/>, in change notifications and on a bound
	/// <see cref="PluginResourceBinding{T}"/>. VPE publishes the game's controller, outputs, displays, segment displays and
	/// audio with IDs below the controller ID, such as <c>pinmame:t2_l8/controller/0/display/dmd0</c>. Descriptors are
	/// immutable; to change one, dispose the publication and publish again with a new <see cref="Instance"/>.
	/// <example>
	/// The B2S plugin publishes its composed backglass for other plugins:
	/// <code>
	/// game.Lifetime.Own(game.Resources.Publish(new PluginResourceDescriptor(game.ControllerId + "/surface/b2s-backglass",
	///     _application.Plugin.Id, game.ControllerId, "B2S backglass", "backglass", ResourceKind.Surface, game.Session, 1), _surface));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginResourceDescriptor
	{
		/// <summary>
		/// Stable logical ID, unique among the resources published at the same time. By convention it starts with the
		/// controller ID (see <see cref="PluginGameContext.ControllerId"/>), so saved choices can store the part after it.
		/// A selection names a resource by this ID in <see cref="PluginResourceSelection.ResourceId"/>.
		/// </summary>
		public string Id { get; }
		/// <summary>
		/// Who published the resource: the game identity's provider (such as <c>pinmame</c>) for resources VPE publishes
		/// from the gamelogic engine, the plugin ID for resources a plugin publishes. Informational; selections do not
		/// filter on it.
		/// </summary>
		public string Provider { get; }
		/// <summary>
		/// ID of the game controller the resource belongs to, normally <see cref="PluginGameContext.ControllerId"/>. Selections
		/// filter on it with <see cref="PluginResourceSelection.Controller"/>.
		/// </summary>
		public string Controller { get; }
		/// <summary>Human-readable name shown in settings, such as the display choices of the DMD and PinUP plugins.</summary>
		public string Label { get; }
		/// <summary>
		/// What the resource is for, such as <c>controller</c>, <c>outputs</c>, <c>dmd</c>, <c>segments</c>,
		/// <c>rom-mix</c> or <c>backglass</c>. Selections filter on it with <see cref="PluginResourceSelection.Role"/>.
		/// </summary>
		public string Role { get; }
		/// <summary>The type of data the resource carries; the published source must implement the matching interface.</summary>
		public ResourceKind Kind { get; }
		/// <summary>
		/// The game session the resource belongs to (see <see cref="PluginGameContext.Session"/>). Selections filter on it,
		/// and a derived resource is only visible while its parents belong to the same session.
		/// </summary>
		public long Session { get; }
		/// <summary>
		/// Generation of this publication. Use a new value every time a resource is published, including when the same
		/// <see cref="Id"/> is published again; VPE uses a process-wide counter. Display frames carry it in
		/// <see cref="PluginDisplayFrame.Instance"/> so bindings drop frames of an earlier publication, and a derived
		/// resource is hidden when a parent is republished with another instance.
		/// </summary>
		public long Instance { get; }
		/// <summary>Version of the resource contract the descriptor follows. Currently always 1.</summary>
		public int ContractVersion { get; } = 1;
		/// <summary>Which version of the content the resource carries; <see cref="ResourceView.Original"/> by default.</summary>
		public ResourceView View { get; }
		/// <summary>
		/// For <see cref="ResourceKind.Display"/> resources, every encoding the source can deliver. A display selection only
		/// matches when it accepts at least one of them. <see cref="DisplayEncoding.None"/> for other kinds.
		/// </summary>
		public DisplayEncoding Formats { get; }
		/// <summary>
		/// IDs of the resources this one is derived from, such as the original DMD a colorized view is computed from; empty
		/// for original resources. Parents must be published first. The registry hides a derived resource while any parent
		/// is missing, has been republished with another <see cref="Instance"/> or belongs to another <see cref="Session"/>.
		/// </summary>
		public IReadOnlyList<string> Parents { get; }
		/// <summary>Creates a descriptor. The parent list is copied.</summary>
		/// <param name="id">The stable logical ID; see <see cref="Id"/>.</param>
		/// <param name="provider">The publisher; see <see cref="Provider"/>.</param>
		/// <param name="controller">The controller ID the resource belongs to.</param>
		/// <param name="label">The human-readable name.</param>
		/// <param name="role">What the resource is for; see <see cref="Role"/>.</param>
		/// <param name="kind">The type of data; must match the source's <see cref="IPluginResourceSource.Kind"/>.</param>
		/// <param name="session">The game session.</param>
		/// <param name="instance">The generation of this publication.</param>
		/// <param name="view">Which version of the content the resource carries.</param>
		/// <param name="formats">For displays, the encodings the source can deliver.</param>
		/// <param name="parents">IDs of the resources this one is derived from.</param>
		/// <exception cref="ArgumentException"><paramref name="id"/> is null, empty or white space.</exception>
		public PluginResourceDescriptor(string id, string provider, string controller, string label, string role, ResourceKind kind,
			long session, long instance, ResourceView view = ResourceView.Original, DisplayEncoding formats = DisplayEncoding.None, params string[] parents)
		{
			if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A resource needs a stable logical ID.", nameof(id));
			Id = id; Provider = provider; Controller = controller; Label = label; Role = role; Kind = kind;
			Session = session; Instance = instance; View = view; Formats = formats; Parents = Array.AsReadOnly((string[])parents.Clone());
		}
	}

	/// <summary>
	/// Describes which resource a consumer wants. Empty strings and a zero session match anything, so a selection names
	/// only what it cares about.
	/// </summary>
	/// <remarks>
	/// Pass it to <see cref="PluginResourceRegistry.Bind{T}"/> to follow the matching resource as it is published, replaced
	/// and removed, or to <see cref="PluginResourceRegistry.Snapshot(PluginResourceSelection)"/> to list the current
	/// matches. Bind copies the selection, so changing it afterwards has no effect on the binding. A binding attaches only
	/// when exactly one resource matches; when several do, set <see cref="ResourceId"/>.
	/// <example>
	/// PinUP binds the game's DMD for raw shades, honouring a display the user picked:
	/// <code>
	/// var displays = new PluginResourceSelection(ResourceKind.Display) {
	///     Controller = game.ControllerId, Role = "dmd", Session = game.Session,
	///     Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray2 | DisplayEncoding.Gray4
	/// };
	/// string selected = game.Settings.String("DmdSource", "auto");
	/// if (selected != "auto") displays.ResourceId = PluginResourceChoices.Resolve(game.ControllerId, selected);
	/// var dmd = game.Lifetime.Own(game.Resources.Bind&lt;IPluginDisplaySource&gt;(displays));
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class PluginResourceSelection
	{
		/// <summary>The kind of resource wanted. Always matched.</summary>
		public ResourceKind Kind { get; }
		/// <summary>
		/// The exact <see cref="PluginResourceDescriptor.Id"/> wanted, or empty (the default) for any. Use it to pick one of
		/// several matching resources, for example from a saved choice through <see cref="PluginResourceChoices.Resolve"/>.
		/// With <see cref="Intent"/> <see cref="ResourceView.Presentation"/>, it names the original display and also matches
		/// the presentation views derived from it, which are then preferred, so a chosen display still shows colorized frames.
		/// </summary>
		public string ResourceId { get; set; } = "";
		/// <summary>The controller ID wanted, normally <see cref="PluginGameContext.ControllerId"/>, or empty for any.</summary>
		public string Controller { get; set; } = "";
		/// <summary>The <see cref="PluginResourceDescriptor.Role"/> wanted, such as <c>dmd</c>, or empty for any.</summary>
		public string Role { get; set; } = "";
		/// <summary>
		/// The game session wanted, normally <see cref="PluginGameContext.Session"/>, or 0 (the default) for any. Set it so a
		/// binding never attaches to a resource of a previous game.
		/// </summary>
		public long Session { get; set; }
		/// <summary>
		/// The view wanted; <see cref="ResourceView.Original"/> by default. <see cref="ResourceView.Original"/> matches
		/// original resources without parents. <see cref="ResourceView.Identification"/> matches resources that are neither
		/// presentation views nor derived, and makes a display binding request raw frames from the source.
		/// <see cref="ResourceView.Presentation"/> matches presentation views, plus originals when
		/// <see cref="AllowOriginalFallback"/> is set.
		/// </summary>
		public ResourceView Intent { get; set; } = ResourceView.Original;
		/// <summary>
		/// For <see cref="ResourceKind.Display"/> selections, the encodings the consumer can read; all five concrete
		/// encodings by default. A display only matches when its <see cref="PluginResourceDescriptor.Formats"/> overlap.
		/// A binding also passes it to <see cref="IPluginDisplaySource.AcquireView"/>, and
		/// <see cref="DisplayBindingExtensions.SubscribeFrames"/> drops frames in other encodings. Ignored for other kinds.
		/// </summary>
		public DisplayEncoding Accepts { get; set; } = DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8 | DisplayEncoding.Rgb24 | DisplayEncoding.Rgba32;
		/// <summary>
		/// With a <see cref="ResourceView.Presentation"/> intent, also match original resources. When no
		/// <see cref="ResourceId"/> is set and presentation views exist, the registry still prefers them, so the consumer
		/// switches between a processed and the original version as producers come and go. The B2S live DMD uses it to show
		/// the DMD plugin's colorized frames when available and the ROM's frames otherwise. False by default.
		/// </summary>
		public bool AllowOriginalFallback { get; set; }
		/// <summary>Creates a selection that matches any resource of a kind; narrow it with the other properties.</summary>
		/// <param name="kind">The kind of resource wanted.</param>
		public PluginResourceSelection(ResourceKind kind) { Kind = kind; }
		/// <summary>Returns an independent copy with the same criteria.</summary>
		public PluginResourceSelection Copy() => new PluginResourceSelection(Kind) {
			ResourceId = ResourceId, Controller = Controller, Role = Role, Session = Session,
			Intent = Intent, Accepts = Accepts, AllowOriginalFallback = AllowOriginalFallback
		};
		/// <summary>A presentation view derived from the chosen resource, wanted when the intent is presentation.</summary>
		private bool PresentsChosen(PluginResourceDescriptor descriptor)
		{
			if (Intent != ResourceView.Presentation || descriptor.View != ResourceView.Presentation) return false;
			foreach (string parent in descriptor.Parents) if (parent == ResourceId) return true;
			return false;
		}
		internal bool Matches(PluginResourceDescriptor descriptor)
		{
			if (descriptor.Kind != Kind || ResourceId.Length > 0 && descriptor.Id != ResourceId && !PresentsChosen(descriptor) || Controller.Length > 0 && descriptor.Controller != Controller
				|| Role.Length > 0 && descriptor.Role != Role || Session != 0 && descriptor.Session != Session) return false;
			if (Kind == ResourceKind.Display && (descriptor.Formats & Accepts) == 0) return false;
			if (Intent == ResourceView.Identification) return descriptor.View != ResourceView.Presentation && descriptor.Parents.Count == 0;
			if (Intent == ResourceView.Presentation) return descriptor.View == ResourceView.Presentation || AllowOriginalFallback && descriptor.View == ResourceView.Original;
			return descriptor.View == ResourceView.Original && descriptor.Parents.Count == 0;
		}
	}

	/// <summary>A resource was published, changed or removed, as reported to <see cref="PluginResourceRegistry.Observe"/> callbacks.</summary>
	/// <remarks>
	/// The B2S plugin observes the registry to attach every segment display of the game as soon as it is published, since
	/// segment displays can appear after the game has started.
	/// </remarks>
	public readonly struct PluginResourceChange
	{
		/// <summary>Whether the resource was added, changed or removed.</summary>
		public ResourceChangeKind Kind { get; }
		/// <summary>The descriptor of the resource that changed.</summary>
		public PluginResourceDescriptor Descriptor { get; }
		/// <summary>Creates a change notification. Called by the registry.</summary>
		/// <param name="kind">What happened.</param>
		/// <param name="descriptor">The resource it happened to.</param>
		public PluginResourceChange(ResourceChangeKind kind, PluginResourceDescriptor descriptor) { Kind = kind; Descriptor = descriptor; }
	}

	/// <summary>
	/// Base of every resource source. A published object implements the interface of its kind, such as
	/// <see cref="IPluginDisplaySource"/>, which derives from this one.
	/// </summary>
	/// <remarks>
	/// Plugins rarely implement the kind interfaces themselves: <see cref="DisplayResourceSource"/>,
	/// <see cref="ControllerResourceSource"/>, <see cref="AudioResourceSource"/>, <see cref="SegmentResourceSource"/>,
	/// <see cref="InputResourceSource"/> and <see cref="TriggerResourceSource"/> handle subscriptions and dispatch.
	/// Surfaces have no ready-made implementation; PinUP and the B2S plugin implement <see cref="IPluginSurface"/>.
	/// </remarks>
	public interface IPluginResourceSource
	{
		/// <summary>
		/// The kind of data this source carries. Must equal the <see cref="PluginResourceDescriptor.Kind"/> it is published with.
		/// </summary>
		ResourceKind Kind { get; }
	}

	/// <summary>One display frame. Borrowed row-major frame memory is valid only during the synchronous subscriber call.</summary>
	/// <remarks>
	/// Producers usually reuse the pixel buffer for the next frame, so a consumer that needs the pixels later must copy them
	/// before returning, as the DMD plugin does when it hands frames to its LibDmd worker. The constructor validates that
	/// the buffer is large enough for the declared shape.
	/// <example>
	/// Publishing a 128x32 frame of 4-bit shades:
	/// <code>
	/// source.Publish(new PluginDisplayFrame(shades, 128, 32, 128, DisplayEncoding.Gray4, ++_sequence, timestampUs, descriptor.Instance));
	/// </code>
	/// </example>
	/// </remarks>
	public readonly struct PluginDisplayFrame
	{
		/// <summary>
		/// The pixels, row by row, each row starting <see cref="Stride"/> bytes after the previous one. Borrowed for the
		/// duration of the subscriber call only.
		/// </summary>
		public ReadOnlyMemory<byte> Pixels { get; }
		/// <summary>Width in pixels; greater than zero.</summary>
		public int Width { get; }
		/// <summary>Height in pixels; greater than zero.</summary>
		public int Height { get; }
		/// <summary>Bytes from the start of one row to the start of the next; at least the width times the bytes per pixel.</summary>
		public int Stride { get; }
		/// <summary>The single concrete encoding of <see cref="Pixels"/>.</summary>
		public DisplayEncoding Encoding { get; }
		/// <summary>The color space of the values; <see cref="FrameColorSpace.Srgb"/> by default.</summary>
		public FrameColorSpace ColorSpace { get; }
		/// <summary>How to interpret alpha for <see cref="DisplayEncoding.Rgba32"/>; <see cref="FrameAlpha.None"/> by default.</summary>
		public FrameAlpha Alpha { get; }
		/// <summary>
		/// Increasing number assigned by the producer. VPE's gamelogic resources share one counter across all their events,
		/// so gaps do not mean dropped frames.
		/// </summary>
		public long Sequence { get; }
		/// <summary>
		/// When the frame was produced, in microseconds of a monotonic clock (VPE uses <c>Stopwatch</c>). Not wall-clock
		/// time; compare only values of the same producer.
		/// </summary>
		public long TimestampUs { get; }
		/// <summary>
		/// The <see cref="PluginResourceDescriptor.Instance"/> of the publication that produced the frame.
		/// <see cref="DisplayBindingExtensions.SubscribeFrames"/> drops frames whose instance differs from the bound one.
		/// </summary>
		public long Instance { get; }
		/// <summary>Creates a frame over a borrowed buffer.</summary>
		/// <param name="pixels">The pixel buffer; at least <paramref name="stride"/> times <paramref name="height"/> bytes.</param>
		/// <param name="width">Width in pixels.</param>
		/// <param name="height">Height in pixels.</param>
		/// <param name="stride">Bytes per row, including any padding.</param>
		/// <param name="encoding">Exactly one concrete encoding.</param>
		/// <param name="sequence">The producer's increasing frame number.</param>
		/// <param name="timestampUs">Monotonic time in microseconds.</param>
		/// <param name="instance">The publication's <see cref="PluginResourceDescriptor.Instance"/>.</param>
		/// <param name="colorSpace">The color space of the values.</param>
		/// <param name="alpha">How to interpret alpha.</param>
		/// <exception cref="ArgumentException">
		/// The encoding is not a single concrete encoding, or the size, stride or buffer length do not fit together.
		/// </exception>
		public PluginDisplayFrame(ReadOnlyMemory<byte> pixels, int width, int height, int stride, DisplayEncoding encoding,
			long sequence, long timestampUs, long instance, FrameColorSpace colorSpace = FrameColorSpace.Srgb, FrameAlpha alpha = FrameAlpha.None)
		{
			int bytes = encoding == DisplayEncoding.Rgb24 ? 3 : encoding == DisplayEncoding.Rgba32 ? 4 : 1;
			if (encoding != DisplayEncoding.Gray2 && encoding != DisplayEncoding.Gray4 && encoding != DisplayEncoding.Luminance8
				&& encoding != DisplayEncoding.Rgb24 && encoding != DisplayEncoding.Rgba32) throw new ArgumentException("A frame needs one concrete VPE encoding.");
			if (width <= 0 || height <= 0 || stride < checked(width * bytes) || pixels.Length < checked(stride * height)) throw new ArgumentException("Display frame shape does not match its buffer.");
			Pixels = pixels; Width = width; Height = height; Stride = stride; Encoding = encoding; Sequence = sequence;
			TimestampUs = timestampUs; Instance = instance; ColorSpace = colorSpace; Alpha = alpha;
		}
	}

	/// <summary>A source of display frames, published as <see cref="ResourceKind.Display"/>.</summary>
	/// <remarks>
	/// VPE publishes each DMD of the game's engine; the DMD plugin publishes a colorized <see cref="ResourceView.Presentation"/>
	/// view derived from it. Producers normally use <see cref="DisplayResourceSource"/>. Consumers should bind through
	/// <see cref="PluginResourceRegistry.Bind{T}"/> and receive frames with
	/// <see cref="DisplayBindingExtensions.SubscribeFrames"/>, which also checks encoding and instance.
	/// <example>
	/// <code>
	/// var dmd = game.Lifetime.Own(game.Resources.Bind&lt;IPluginDisplaySource&gt;(selection));
	/// game.Lifetime.Own(dmd.SubscribeFrames(frame =&gt; Render(frame.Pixels.Span, frame.Width, frame.Height, frame.Stride)));
	/// </code>
	/// </example>
	/// </remarks>
	public interface IPluginDisplaySource : IPluginResourceSource
	{
		/// <summary>
		/// Registers a receiver for every frame. It is called synchronously on the producer's thread (the main thread for
		/// VPE's displays) and must not keep the borrowed pixels.
		/// </summary>
		/// <param name="receive">Called with each frame.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		IDisposable Subscribe(Action<PluginDisplayFrame> receive);
		/// <summary>
		/// Declares the view a consumer needs while it is attached; bindings call it for you. Acquiring an identification
		/// view owns format negotiation until the returned lease is disposed: the source switches to raw shades the consumer
		/// accepts (see <see cref="DisplayFormatNegotiator"/>). Other views do not change the source.
		/// </summary>
		/// <param name="intent">The view the consumer wants.</param>
		/// <param name="acceptedFormats">The encodings the consumer can read.</param>
		/// <returns>A lease; disposing it releases the request.</returns>
		/// <exception cref="InvalidOperationException">
		/// The source cannot deliver an identification view in an accepted encoding, for example because another consumer
		/// already holds it in an encoding this one does not accept. A binding then becomes
		/// <see cref="ResourceBindingState.Blocked"/>.
		/// </exception>
		IDisposable AcquireView(ResourceView intent, DisplayEncoding acceptedFormats);
	}

	/// <summary>One event of a game controller: a switch, coil, lamp, GI, mech or end-of-poll notification.</summary>
	/// <remarks>
	/// Events of a <see cref="ResourceKind.Controller"/> resource are the table's mapped events: <see cref="Id"/> is the
	/// engine's ID of the switch, coil or lamp, <see cref="Number"/> is -1 and <see cref="Value"/> is the engine's value
	/// (0 or 1 for switches, the 0..1 strength for coils, the engine's own range for lamps such as 0..255 for PinMAME).
	/// <para>
	/// Events of an <see cref="ResourceKind.Outputs"/> resource address outputs by the controller's own numbering
	/// (<see cref="Number"/>) and describe their raw value per address, because one controller can mix binary and
	/// physically faded outputs of the same kind. Each poll ends with a <see cref="ControllerEventKind.PollEnd"/> event.
	/// </para>
	/// <example>
	/// The B2S backglass normalizes a native lamp to 0..1:
	/// <code>
	/// if (value.Kind == ControllerEventKind.Lamp &amp;&amp; value.Number &gt;= 0) SetLamp(value.Number, (float)(value.Value / value.Maximum));
	/// </code>
	/// </example>
	/// </remarks>
	public readonly struct PluginControllerEvent
	{
		/// <summary>What the event reports.</summary>
		public ControllerEventKind Kind { get; }
		/// <summary>
		/// The output's ID: the engine's switch, coil or lamp ID for mapped events, the native number as text for native
		/// ones, and empty for <see cref="ControllerEventKind.PollEnd"/>.
		/// </summary>
		public string Id { get; }
		/// <summary>The raw value; see the type remarks for mapped events and <see cref="Semantics"/> for native ones.</summary>
		public double Value { get; }
		/// <summary>
		/// Increasing number assigned by the producer. VPE shares one counter across all events of a game's resources.
		/// </summary>
		public long Sequence { get; }
		/// <summary>When the event was emitted, in microseconds of a monotonic clock; not wall-clock time.</summary>
		public long TimestampUs { get; }
		/// <summary>Controller-native output number, or -1 when the event is addressed by <see cref="Id"/> only.</summary>
		public int Number { get; }
		/// <summary>
		/// What <see cref="Value"/> means. Only meaningful for native events; mapped events are created with
		/// <see cref="ControllerValueSemantics.Binary"/> whatever their actual range.
		/// </summary>
		public ControllerValueSemantics Semantics { get; }
		/// <summary>
		/// Raw value of a fully on output (or the largest level); 1 for binary outputs. Always greater than zero: the
		/// constructor replaces zero or negative values with 1.
		/// </summary>
		public double Maximum { get; }
		/// <summary>Creates a mapped event, addressed by ID, with no native number, binary semantics and a maximum of 1.</summary>
		/// <param name="kind">What the event reports.</param>
		/// <param name="id">The engine's ID of the output.</param>
		/// <param name="value">The engine's value.</param>
		/// <param name="sequence">The producer's increasing event number.</param>
		/// <param name="timestampUs">Monotonic time in microseconds.</param>
		public PluginControllerEvent(ControllerEventKind kind, string id, double value, long sequence, long timestampUs)
			: this(kind, id, value, sequence, timestampUs, -1, ControllerValueSemantics.Binary, 1) { }
		/// <summary>Creates an event addressed by native number, with its value semantics.</summary>
		/// <param name="kind">What the event reports.</param>
		/// <param name="id">The output's ID, usually the number as text.</param>
		/// <param name="value">The raw value.</param>
		/// <param name="sequence">The producer's increasing event number.</param>
		/// <param name="timestampUs">Monotonic time in microseconds.</param>
		/// <param name="number">The controller-native output number, or -1.</param>
		/// <param name="semantics">What <paramref name="value"/> means.</param>
		/// <param name="maximum">The value of a fully on output; values of zero or below become 1.</param>
		public PluginControllerEvent(ControllerEventKind kind, string id, double value, long sequence, long timestampUs, int number, ControllerValueSemantics semantics, double maximum)
		{ Kind = kind; Id = id; Value = value; Sequence = sequence; TimestampUs = timestampUs; Number = number; Semantics = semantics; Maximum = maximum > 0 ? maximum : 1; }
	}
	/// <summary>
	/// A source of controller events, published as <see cref="ResourceKind.Controller"/> (mapped events) or
	/// <see cref="ResourceKind.Outputs"/> (native outputs).
	/// </summary>
	/// <remarks>
	/// VPE publishes both for the game's engine using <see cref="ControllerResourceSource"/>. PinUP subscribes to the mapped
	/// events to fire PUP triggers; the B2S backglass prefers the native outputs and falls back to mapped events.
	/// <example>
	/// <code>
	/// var controller = game.Lifetime.Own(game.Resources.Bind&lt;IPluginControllerSource&gt;(
	///     new PluginResourceSelection(ResourceKind.Controller) { Controller = game.ControllerId, Session = game.Session }));
	/// game.Lifetime.Own(controller.Connect(source =&gt; source.Subscribe(OnControllerEvent)));
	/// </code>
	/// </example>
	/// </remarks>
	public interface IPluginControllerSource : IPluginResourceSource
	{
		/// <summary>
		/// Registers a receiver for every event. It is called synchronously on the producer's thread; VPE publishes on the
		/// main thread.
		/// </summary>
		/// <param name="receive">Called with each event.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		IDisposable Subscribe(Action<PluginControllerEvent> receive);
	}

	/// <summary>One block of PCM audio. The sample memory is borrowed for the synchronous subscriber call only.</summary>
	/// <remarks>
	/// Samples are interleaved: each sample frame holds one sample per channel. PinMAME publishes every block its sound
	/// board renders as <see cref="PcmEncoding.Float32"/>, from the emulator's audio callback rather than the main thread.
	/// <example>
	/// <code>
	/// int length = frames * channels * sizeof(float);
	/// source.Publish(new PluginPcmFrame(buffer.AsMemory(0, length), PcmEncoding.Float32, 48000, channels, frames, ++_sequence));
	/// </code>
	/// </example>
	/// </remarks>
	public readonly struct PluginPcmFrame
	{
		/// <summary>The interleaved samples, exactly <see cref="Frames"/> times <see cref="Channels"/> samples long.</summary>
		public ReadOnlyMemory<byte> Samples { get; }
		/// <summary>The sample format.</summary>
		public PcmEncoding Encoding { get; }
		/// <summary>Sample frames per second, in Hz; greater than zero.</summary>
		public int SampleRate { get; }
		/// <summary>Number of interleaved channels; greater than zero.</summary>
		public int Channels { get; }
		/// <summary>Number of sample frames in the block (samples per channel); zero or more.</summary>
		public int Frames { get; }
		/// <summary>Increasing block number assigned by the producer.</summary>
		public long Sequence { get; }
		/// <summary>When the block was produced, in microseconds of a monotonic clock, or 0 when unknown (PinMAME leaves it 0).</summary>
		public long TimestampUs { get; }
		/// <summary>Free-form description of the channel order, or empty when unspecified.</summary>
		public string ChannelLayout { get; }
		/// <summary>Creates a block over borrowed samples.</summary>
		/// <param name="samples">The interleaved samples.</param>
		/// <param name="encoding">The sample format.</param>
		/// <param name="sampleRate">Sample frames per second.</param>
		/// <param name="channels">Number of interleaved channels.</param>
		/// <param name="frames">Number of sample frames.</param>
		/// <param name="sequence">The producer's increasing block number.</param>
		/// <param name="timestampUs">Monotonic time in microseconds, or 0 when unknown.</param>
		/// <param name="channelLayout">Description of the channel order, or empty.</param>
		/// <exception cref="ArgumentException">
		/// The rate or channel count is not positive, the frame count is negative, or the sample length does not equal
		/// frames times channels times bytes per sample.
		/// </exception>
		public PluginPcmFrame(ReadOnlyMemory<byte> samples, PcmEncoding encoding, int sampleRate, int channels, int frames, long sequence, long timestampUs = 0, string channelLayout = "")
		{
			if (sampleRate <= 0 || channels <= 0 || frames < 0 || samples.Length != checked(frames * channels * (encoding == PcmEncoding.Float32 ? 4 : 2)))
				throw new ArgumentException("PCM frame shape does not match its samples.");
			Samples = samples; Encoding = encoding; SampleRate = sampleRate; Channels = channels; Frames = frames; Sequence = sequence; TimestampUs = timestampUs; ChannelLayout = channelLayout;
		}
	}
	/// <summary>A source of PCM audio, published as <see cref="ResourceKind.Audio"/>.</summary>
	/// <remarks>
	/// VPE publishes the engine's own sound, such as PinMAME's ROM audio, as <c>&lt;controller&gt;/audio/rom</c> with the
	/// role <c>rom-mix</c>. Producers normally use <see cref="AudioResourceSource"/>, which also implements
	/// <see cref="IPluginAudioPlaybackControl"/>.
	/// </remarks>
	public interface IPluginAudioSource : IPluginResourceSource
	{
		/// <summary>
		/// Registers a receiver for every block. It is called synchronously on the producer's thread, which for PinMAME is
		/// the emulator's audio thread, so it must be fast, thread-safe and copy any samples it keeps.
		/// </summary>
		/// <param name="receive">Called with each block.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		IDisposable Subscribe(Action<PluginPcmFrame> receive);
	}
	/// <summary>An exclusive lease replaces built-in playback while leaving the original PCM stream available.</summary>
	/// <remarks>
	/// Implemented by audio sources whose producer can stop playing its sound itself, such as
	/// <see cref="AudioResourceSource"/>. A plugin that plays or processes the sound on its own (for example to send it to a
	/// different device) casts the bound source to this interface and holds a lease for as long as it plays; PinMAME then
	/// silences its own output but keeps publishing PCM.
	/// <example>
	/// <code>
	/// var audio = game.Lifetime.Own(game.Resources.Bind&lt;IPluginAudioSource&gt;(new PluginResourceSelection(ResourceKind.Audio) {
	///     Controller = game.ControllerId, Role = "rom-mix", Session = game.Session }));
	/// if (audio.Source is IPluginAudioPlaybackControl control) game.Lifetime.Own(control.AcquirePlayback(_application.Plugin.Id));
	/// </code>
	/// </example>
	/// </remarks>
	public interface IPluginAudioPlaybackControl
	{
		/// <summary>Takes over playback until the returned lease is disposed, which restores the producer's own playback.</summary>
		/// <param name="pluginId">ID of the plugin taking over, reported to a plugin that tries to take over as well.</param>
		/// <returns>The lease.</returns>
		/// <exception cref="InvalidOperationException">Another plugin already holds the lease.</exception>
		IDisposable AcquirePlayback(string pluginId);
	}
	/// <summary>A segment display. Its place and size are known when it is published, before any frame arrives.</summary>
	/// <remarks>
	/// VPE publishes one per segment display the engine announces, as <c>&lt;controller&gt;/segments/&lt;id&gt;</c> with the
	/// role <c>segments</c>, using <see cref="SegmentResourceSource"/>. The B2S backglass concatenates the digits of all
	/// displays in <see cref="Order"/> onto its score digits, so it needs every display's position and size before the
	/// first frame of any of them arrives.
	/// </remarks>
	public interface IPluginSegmentSource : IPluginResourceSource
	{
		/// <summary>Position among the controller's segment displays, in the order the controller announced them, from 0.</summary>
		int Order { get; }
		/// <summary>
		/// Number of elements (digits) the controller announced; frames carry the same count. 0 when the producer did not
		/// announce a size, in which case consumers learn it from the first frame.
		/// </summary>
		int Elements { get; }
		/// <summary>Segment arrangement of the elements, as in <see cref="PluginSegmentFrame.Layout"/>.</summary>
		string Layout { get; }
		/// <summary>
		/// Display technology hint, as in <see cref="PluginSegmentFrame.Hardware"/>: a free-form, case-sensitive string made
		/// of tokens. It may hold a technology token such as <c>plasma</c>, <c>vfd-green</c>, <c>vfd-blue</c> or <c>led</c>,
		/// and/or <c>gottlieb</c> for upright Gottlieb System 80 digits. Empty means unknown. Match tokens with
		/// <c>Contains</c> or <c>StartsWith</c>, not equality.
		/// </summary>
		string Hardware { get; }
		/// <summary>
		/// Registers a receiver for every frame. It is called synchronously on the producer's thread; VPE publishes on the
		/// main thread.
		/// </summary>
		/// <param name="receive">Called with each frame.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		IDisposable Subscribe(Action<PluginSegmentFrame> receive);
	}
	/// <summary>The state of every segment of a segment display at one moment.</summary>
	/// <remarks>
	/// <see cref="Layout"/> names the segment arrangement of the elements: <c>seg7</c> (a-g and a comma at bit 7),
	/// <c>seg9</c> (seven segments plus split verticals at bits 8 and 9), <c>seg14</c> (a-f, g1, comma, h, i, j, g2,
	/// k, l, m, dot) or <c>seg16</c>. <see cref="Hardware"/> is a free-form hint about the display technology. Brightness
	/// is borrowed for the synchronous callback only.
	/// <para>
	/// VPE publishes PinMAME's segment displays with 16 segments per element, one per bit of PinMAME's 16-bit segment mask,
	/// each fully on (1) or off (0).
	/// </para>
	/// </remarks>
	public readonly struct PluginSegmentFrame
	{
		/// <summary>The segment arrangement: <c>seg7</c>, <c>seg9</c>, <c>seg14</c> or <c>seg16</c>; see the type remarks.</summary>
		public string Layout { get; }
		/// <summary>Number of elements (digits); greater than zero.</summary>
		public int Elements { get; }
		/// <summary>
		/// Number of brightness values per element; greater than zero. VPE always sends 16, and the B2S backglass ignores
		/// frames with another count.
		/// </summary>
		public int SegmentsPerElement { get; }
		/// <summary>
		/// Brightness of each segment from 0 (off) to 1 (fully lit), element by element: segment <c>s</c> of element
		/// <c>e</c> is at index <c>e * SegmentsPerElement + s</c>. Borrowed for the duration of the subscriber call only.
		/// </summary>
		public ReadOnlyMemory<float> Brightness { get; }
		/// <summary>
		/// Display technology hint: a free-form, case-sensitive string made of tokens. It may hold a technology token such
		/// as <c>plasma</c>, <c>vfd-green</c>, <c>vfd-blue</c> or <c>led</c>, and/or <c>gottlieb</c> for upright Gottlieb
		/// System 80 digits. Empty (never null) means unknown. Match tokens with <c>Contains</c> or <c>StartsWith</c>, not
		/// equality. PinMAME sends <c>gottlieb</c> for Gottlieb System 80 and an empty string otherwise; the B2S backglass
		/// picks its style from a <c>vfd-green</c>, <c>vfd-blue</c> or <c>led</c> prefix (plasma otherwise) and draws
		/// upright digits when the hint contains <c>gottlieb</c>.
		/// </summary>
		public string Hardware { get; }
		/// <summary>Creates a frame without a hardware hint.</summary>
		/// <param name="layout">The segment arrangement.</param>
		/// <param name="elements">Number of elements.</param>
		/// <param name="segmentsPerElement">Number of brightness values per element.</param>
		/// <param name="brightness">The borrowed brightness values, exactly elements times segments per element long.</param>
		/// <exception cref="ArgumentException">A count is not positive or the brightness length does not match.</exception>
		public PluginSegmentFrame(string layout, int elements, int segmentsPerElement, ReadOnlyMemory<float> brightness) : this(layout, elements, segmentsPerElement, brightness, "") { }
		/// <summary>Creates a frame with a hardware hint.</summary>
		/// <param name="layout">The segment arrangement.</param>
		/// <param name="elements">Number of elements.</param>
		/// <param name="segmentsPerElement">Number of brightness values per element.</param>
		/// <param name="brightness">The borrowed brightness values, exactly elements times segments per element long.</param>
		/// <param name="hardware">The technology hint; null becomes empty.</param>
		/// <exception cref="ArgumentException">A count is not positive or the brightness length does not match.</exception>
		public PluginSegmentFrame(string layout, int elements, int segmentsPerElement, ReadOnlyMemory<float> brightness, string hardware)
		{
			if (elements <= 0 || segmentsPerElement <= 0 || brightness.Length != checked(elements * segmentsPerElement)) throw new ArgumentException("Segment layout does not match its state.");
			Layout = layout; Elements = elements; SegmentsPerElement = segmentsPerElement; Brightness = brightness; Hardware = hardware ?? "";
		}
	}
	/// <summary>One player input action, such as a nudge axis or a button.</summary>
	/// <remarks>
	/// Carried by <see cref="IPluginInputSource"/> and transformed by <see cref="IPluginInputFilter"/>s in a
	/// <see cref="PluginInputPipeline"/>. Nothing in VPE produces input events for plugins yet.
	/// </remarks>
	public readonly struct PluginInputEvent
	{
		/// <summary>Name of the action, such as <c>nudge-x</c>.</summary>
		public string Action { get; }
		/// <summary>The action's value, such as an axis position or 1 and 0 for a pressed and released button.</summary>
		public float Value { get; }
		/// <summary>When the input happened, in microseconds of a monotonic clock.</summary>
		public long TimestampUs { get; }
		/// <summary>Creates an input event.</summary>
		/// <param name="action">Name of the action.</param>
		/// <param name="value">The action's value.</param>
		/// <param name="timestampUs">Monotonic time in microseconds.</param>
		public PluginInputEvent(string action, float value, long timestampUs) { Action = action; Value = value; TimestampUs = timestampUs; }
	}
	/// <summary>
	/// A source of player input events, published as <see cref="ResourceKind.Input"/>. Producers can use
	/// <see cref="InputResourceSource"/>.
	/// </summary>
	public interface IPluginInputSource : IPluginResourceSource
	{
		/// <summary>Registers a receiver for every event, called synchronously on the producer's thread.</summary>
		/// <param name="receive">Called with each event.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		IDisposable Subscribe(Action<PluginInputEvent> receive);
	}
	/// <summary>Transforms an input event before it reaches the game, registered in a <see cref="PluginInputPipeline"/>.</summary>
	/// <remarks>
	/// Filters run by the order given at registration, then by plugin ID; each receives the previous filter's result.
	/// <example>
	/// A filter that halves nudge strength:
	/// <code>
	/// public PluginInputEvent Filter(in PluginInputEvent input)
	///     =&gt; input.Action == "nudge-x" ? new PluginInputEvent(input.Action, input.Value * 0.5f, input.TimestampUs) : input;
	/// </code>
	/// </example>
	/// </remarks>
	public interface IPluginInputFilter
	{
		/// <summary>Returns the event to pass on; return <paramref name="input"/> unchanged to let it through.</summary>
		/// <param name="input">The event as the previous filter left it.</param>
		PluginInputEvent Filter(in PluginInputEvent input);
	}
	/// <summary>An image other plugins can show, published as <see cref="ResourceKind.Surface"/>.</summary>
	/// <remarks>
	/// The interface only describes the image; <see cref="Backend"/> tells consumers which richer interface gives access to
	/// it. PinUP publishes each PUP screen and the B2S plugin its composed backglass with the backend
	/// <c>unity-render-texture</c>, whose sources implement the Unity integration's <c>IUnityTextureResource</c>.
	/// </remarks>
	public interface IPluginSurface : IPluginResourceSource
	{
		/// <summary>Current width in pixels; 0 while the producer has no image yet.</summary>
		int Width { get; }
		/// <summary>Current height in pixels; 0 while the producer has no image yet.</summary>
		int Height { get; }
		/// <summary>
		/// How the image is accessed, such as <c>unity-render-texture</c>. Consumers check it before casting the source to
		/// the matching interface.
		/// </summary>
		string Backend { get; }
	}
	/// <summary>One named trigger with an integer value, carried by <see cref="IPluginTriggerSource"/>.</summary>
	/// <remarks>Nothing in VPE publishes trigger resources yet.</remarks>
	public readonly struct PluginTriggerEvent
	{
		/// <summary>Name of the trigger.</summary>
		public string Id { get; }
		/// <summary>The trigger's value.</summary>
		public int Value { get; }
		/// <summary>Increasing number assigned by the producer.</summary>
		public long Sequence { get; }
		/// <summary>When the trigger fired, in microseconds of a monotonic clock.</summary>
		public long TimestampUs { get; }
		/// <summary>Creates a trigger event.</summary>
		/// <param name="id">Name of the trigger.</param>
		/// <param name="value">The trigger's value.</param>
		/// <param name="sequence">The producer's increasing event number.</param>
		/// <param name="timestampUs">Monotonic time in microseconds.</param>
		public PluginTriggerEvent(string id, int value, long sequence, long timestampUs) { Id = id; Value = value; Sequence = sequence; TimestampUs = timestampUs; }
	}
	/// <summary>
	/// A source of trigger events, published as <see cref="ResourceKind.Triggers"/>. Producers can use
	/// <see cref="TriggerResourceSource"/>.
	/// </summary>
	public interface IPluginTriggerSource : IPluginResourceSource
	{
		/// <summary>Registers a receiver for every event, called synchronously on the producer's thread.</summary>
		/// <param name="receive">Called with each event.</param>
		/// <returns>Disposing it stops the receiver.</returns>
		IDisposable Subscribe(Action<PluginTriggerEvent> receive);
	}
}
