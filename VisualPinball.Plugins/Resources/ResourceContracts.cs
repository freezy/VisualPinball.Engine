// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <remarks>
	/// <see cref="Outputs"/> carries every controller output by its native number through <see cref="IPluginControllerSource"/>.
	/// It is separate from <see cref="Controller"/>, the outputs as the table maps them, so a controller binding without a
	/// role stays unambiguous.
	/// </remarks>
	public enum ResourceKind { Controller, Display, Segments, Audio, Input, Surface, Triggers, Outputs }
	public enum ResourceView { Original, Identification, Presentation }
	public enum ResourceBindingState { Waiting, Bound, Blocked, Disposed }
	public enum ResourceChangeKind { Added, Changed, Removed }
	[Flags] public enum DisplayEncoding { None = 0, Gray2 = 1, Gray4 = 2, Luminance8 = 4, Rgb24 = 8, Rgba32 = 16 }
	public enum FrameColorSpace { Linear, Srgb }
	public enum FrameAlpha { None, Straight, Premultiplied }
	public enum PcmEncoding { Int16LittleEndian, Float32 }
	public enum ControllerEventKind
	{
		Switch, Coil, Lamp, GeneralIllumination, Trigger, State,
		/// <summary>Raw position of an emulated mech, as the controller reports it.</summary>
		Mech,
		/// <summary>Ends one controller poll; consumers that batch per poll apply what they collected.</summary>
		PollEnd
	}
	/// <summary>What a controller event's raw value means.</summary>
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

	public sealed class PluginResourceDescriptor
	{
		public string Id { get; }
		public string Provider { get; }
		public string Controller { get; }
		public string Label { get; }
		public string Role { get; }
		public ResourceKind Kind { get; }
		public long Session { get; }
		public long Instance { get; }
		public int ContractVersion { get; } = 1;
		public ResourceView View { get; }
		public DisplayEncoding Formats { get; }
		public IReadOnlyList<string> Parents { get; }
		public PluginResourceDescriptor(string id, string provider, string controller, string label, string role, ResourceKind kind,
			long session, long instance, ResourceView view = ResourceView.Original, DisplayEncoding formats = DisplayEncoding.None, params string[] parents)
		{
			if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A resource needs a stable logical ID.", nameof(id));
			Id = id; Provider = provider; Controller = controller; Label = label; Role = role; Kind = kind;
			Session = session; Instance = instance; View = view; Formats = formats; Parents = Array.AsReadOnly((string[])parents.Clone());
		}
	}

	public sealed class PluginResourceSelection
	{
		public ResourceKind Kind { get; }
		public string ResourceId { get; set; } = "";
		public string Controller { get; set; } = "";
		public string Role { get; set; } = "";
		public long Session { get; set; }
		public ResourceView Intent { get; set; } = ResourceView.Original;
		public DisplayEncoding Accepts { get; set; } = DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8 | DisplayEncoding.Rgb24 | DisplayEncoding.Rgba32;
		public bool AllowOriginalFallback { get; set; }
		public PluginResourceSelection(ResourceKind kind) { Kind = kind; }
		public PluginResourceSelection Copy() => new PluginResourceSelection(Kind) {
			ResourceId = ResourceId, Controller = Controller, Role = Role, Session = Session,
			Intent = Intent, Accepts = Accepts, AllowOriginalFallback = AllowOriginalFallback
		};
		internal bool Matches(PluginResourceDescriptor descriptor)
		{
			if (descriptor.Kind != Kind || ResourceId.Length > 0 && descriptor.Id != ResourceId || Controller.Length > 0 && descriptor.Controller != Controller
				|| Role.Length > 0 && descriptor.Role != Role || Session != 0 && descriptor.Session != Session) return false;
			if (Kind == ResourceKind.Display && (descriptor.Formats & Accepts) == 0) return false;
			if (Intent == ResourceView.Identification) return descriptor.View != ResourceView.Presentation && descriptor.Parents.Count == 0;
			if (Intent == ResourceView.Presentation) return descriptor.View == ResourceView.Presentation || AllowOriginalFallback && descriptor.View == ResourceView.Original;
			return descriptor.View == ResourceView.Original && descriptor.Parents.Count == 0;
		}
	}

	public readonly struct PluginResourceChange
	{
		public ResourceChangeKind Kind { get; }
		public PluginResourceDescriptor Descriptor { get; }
		public PluginResourceChange(ResourceChangeKind kind, PluginResourceDescriptor descriptor) { Kind = kind; Descriptor = descriptor; }
	}

	public interface IPluginResourceSource { ResourceKind Kind { get; } }

	/// <summary>Borrowed row-major frame memory is valid only during the synchronous subscriber call.</summary>
	public readonly struct PluginDisplayFrame
	{
		public ReadOnlyMemory<byte> Pixels { get; }
		public int Width { get; }
		public int Height { get; }
		public int Stride { get; }
		public DisplayEncoding Encoding { get; }
		public FrameColorSpace ColorSpace { get; }
		public FrameAlpha Alpha { get; }
		public long Sequence { get; }
		public long TimestampUs { get; }
		public long Instance { get; }
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

	public interface IPluginDisplaySource : IPluginResourceSource
	{
		IDisposable Subscribe(Action<PluginDisplayFrame> receive);
		/// <summary>Acquiring an identification view owns format negotiation until the returned lease is disposed.</summary>
		IDisposable AcquireView(ResourceView intent, DisplayEncoding acceptedFormats);
	}

	/// <remarks>
	/// Events of a <c>controller-native</c> resource address outputs by the controller's own numbering
	/// (<see cref="Number"/>) and describe their raw value per address, because one controller can mix binary and
	/// physically faded outputs of the same kind.
	/// </remarks>
	public readonly struct PluginControllerEvent
	{
		public ControllerEventKind Kind { get; }
		public string Id { get; }
		public double Value { get; }
		public long Sequence { get; }
		public long TimestampUs { get; }
		/// <summary>Controller-native output number, or -1 when the event is addressed by <see cref="Id"/> only.</summary>
		public int Number { get; }
		public ControllerValueSemantics Semantics { get; }
		/// <summary>Raw value of a fully on output (or the largest level); 1 for binary outputs.</summary>
		public double Maximum { get; }
		public PluginControllerEvent(ControllerEventKind kind, string id, double value, long sequence, long timestampUs)
			: this(kind, id, value, sequence, timestampUs, -1, ControllerValueSemantics.Binary, 1) { }
		public PluginControllerEvent(ControllerEventKind kind, string id, double value, long sequence, long timestampUs, int number, ControllerValueSemantics semantics, double maximum)
		{ Kind = kind; Id = id; Value = value; Sequence = sequence; TimestampUs = timestampUs; Number = number; Semantics = semantics; Maximum = maximum > 0 ? maximum : 1; }
	}
	public interface IPluginControllerSource : IPluginResourceSource { IDisposable Subscribe(Action<PluginControllerEvent> receive); }

	public readonly struct PluginPcmFrame
	{
		public ReadOnlyMemory<byte> Samples { get; }
		public PcmEncoding Encoding { get; }
		public int SampleRate { get; }
		public int Channels { get; }
		public int Frames { get; }
		public long Sequence { get; }
		public long TimestampUs { get; }
		public string ChannelLayout { get; }
		public PluginPcmFrame(ReadOnlyMemory<byte> samples, PcmEncoding encoding, int sampleRate, int channels, int frames, long sequence, long timestampUs = 0, string channelLayout = "")
		{
			if (sampleRate <= 0 || channels <= 0 || frames < 0 || samples.Length != checked(frames * channels * (encoding == PcmEncoding.Float32 ? 4 : 2)))
				throw new ArgumentException("PCM frame shape does not match its samples.");
			Samples = samples; Encoding = encoding; SampleRate = sampleRate; Channels = channels; Frames = frames; Sequence = sequence; TimestampUs = timestampUs; ChannelLayout = channelLayout;
		}
	}
	public interface IPluginAudioSource : IPluginResourceSource { IDisposable Subscribe(Action<PluginPcmFrame> receive); }
	/// <summary>An exclusive lease replaces built-in playback while leaving the original PCM stream available.</summary>
	public interface IPluginAudioPlaybackControl { IDisposable AcquirePlayback(string pluginId); }
	/// <summary>A segment display. Its place and size are known when it is published, before any frame arrives.</summary>
	public interface IPluginSegmentSource : IPluginResourceSource
	{
		/// <summary>Position among the controller's segment displays, in the order the controller announced them.</summary>
		int Order { get; }
		/// <summary>Number of elements (digits) the controller announced; frames carry the same count.</summary>
		int Elements { get; }
		IDisposable Subscribe(Action<PluginSegmentFrame> receive);
	}
	/// <remarks>
	/// <see cref="Layout"/> names the segment arrangement of the elements: <c>seg7</c> (a-g and a comma at bit 7),
	/// <c>seg9</c> (seven segments plus split verticals at bits 8 and 9), <c>seg14</c> (a-f, g1, comma, h, i, j, g2,
	/// k, l, m, dot) or <c>seg16</c>. <see cref="Hardware"/> hints at the display technology: <c>plasma</c>,
	/// <c>vfd-green</c>, <c>vfd-blue</c>, <c>led</c>, optionally followed by <c>+gottlieb</c>. Brightness is borrowed
	/// for the synchronous callback only.
	/// </remarks>
	public readonly struct PluginSegmentFrame
	{
		public string Layout { get; }
		public int Elements { get; }
		public int SegmentsPerElement { get; }
		public ReadOnlyMemory<float> Brightness { get; }
		public string Hardware { get; }
		public PluginSegmentFrame(string layout, int elements, int segmentsPerElement, ReadOnlyMemory<float> brightness) : this(layout, elements, segmentsPerElement, brightness, "") { }
		public PluginSegmentFrame(string layout, int elements, int segmentsPerElement, ReadOnlyMemory<float> brightness, string hardware)
		{
			if (elements <= 0 || segmentsPerElement <= 0 || brightness.Length != checked(elements * segmentsPerElement)) throw new ArgumentException("Segment layout does not match its state.");
			Layout = layout; Elements = elements; SegmentsPerElement = segmentsPerElement; Brightness = brightness; Hardware = hardware ?? "";
		}
	}
	public readonly struct PluginInputEvent
	{
		public string Action { get; }
		public float Value { get; }
		public long TimestampUs { get; }
		public PluginInputEvent(string action, float value, long timestampUs) { Action = action; Value = value; TimestampUs = timestampUs; }
	}
	public interface IPluginInputSource : IPluginResourceSource { IDisposable Subscribe(Action<PluginInputEvent> receive); }
	public interface IPluginInputFilter { PluginInputEvent Filter(in PluginInputEvent input); }
	public interface IPluginSurface : IPluginResourceSource { int Width { get; } int Height { get; } string Backend { get; } }
	public readonly struct PluginTriggerEvent
	{
		public string Id { get; }
		public int Value { get; }
		public long Sequence { get; }
		public long TimestampUs { get; }
		public PluginTriggerEvent(string id, int value, long sequence, long timestampUs) { Id = id; Value = value; Sequence = sequence; TimestampUs = timestampUs; }
	}
	public interface IPluginTriggerSource : IPluginResourceSource { IDisposable Subscribe(Action<PluginTriggerEvent> receive); }
}
