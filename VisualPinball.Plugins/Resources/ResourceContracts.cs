// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// See LICENSE for the terms of use and distribution.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	public enum ResourceKind { Controller, Display, Segments, Audio, Input, Surface, Triggers }
	public enum ResourceView { Original, Identification, Presentation }
	public enum ResourceBindingState { Waiting, Bound, Blocked, Disposed }
	public enum ResourceChangeKind { Added, Changed, Removed }
	[Flags] public enum DisplayEncoding { None = 0, Gray2 = 1, Gray4 = 2, Luminance8 = 4, Rgb24 = 8, Rgba32 = 16 }
	public enum FrameColorSpace { Linear, Srgb }
	public enum FrameAlpha { None, Straight, Premultiplied }
	public enum PcmEncoding { Int16LittleEndian, Float32 }
	public enum ControllerEventKind { Switch, Coil, Lamp, GeneralIllumination, Trigger, State }

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

	public readonly struct PluginControllerEvent
	{
		public ControllerEventKind Kind { get; }
		public string Id { get; }
		public double Value { get; }
		public long Sequence { get; }
		public long TimestampUs { get; }
		public PluginControllerEvent(ControllerEventKind kind, string id, double value, long sequence, long timestampUs)
		{ Kind = kind; Id = id; Value = value; Sequence = sequence; TimestampUs = timestampUs; }
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
	public interface IPluginSegmentSource : IPluginResourceSource { IDisposable Subscribe(Action<PluginSegmentFrame> receive); }
	public readonly struct PluginSegmentFrame
	{
		public string Layout { get; }
		public int Elements { get; }
		public int SegmentsPerElement { get; }
		public ReadOnlyMemory<float> Brightness { get; }
		public PluginSegmentFrame(string layout, int elements, int segmentsPerElement, ReadOnlyMemory<float> brightness)
		{
			if (elements <= 0 || segmentsPerElement <= 0 || brightness.Length != checked(elements * segmentsPerElement)) throw new ArgumentException("Segment layout does not match its state.");
			Layout = layout; Elements = elements; SegmentsPerElement = segmentsPerElement; Brightness = brightness;
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
