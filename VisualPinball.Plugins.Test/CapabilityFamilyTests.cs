// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VisualPinball.Plugins;

namespace VisualPinball.Plugins.Test
{
	[TestFixture]
	public sealed class CapabilityFamilyTests
	{
		[Test]
		public void AudioReplacementKeepsOriginalStreamAvailableAndRestoresPlayback()
		{
			using var source = new AudioResourceSource(); var registry = new PluginResourceRegistry();
			using var publication = registry.Publish(new PluginResourceDescriptor("rom/audio", "rom", "controller", "ROM mix", "rom-mix", ResourceKind.Audio, 1, 1), source);
			using var binding = registry.Bind<IPluginAudioSource>(new PluginResourceSelection(ResourceKind.Audio) { Controller = "controller", Session = 1 });
			int count = 0; using var subscription = binding.Connect(audio => audio.Subscribe(frame => { count++; Assert.That(frame.Channels, Is.EqualTo(2)); }));
			using var replacement = ((IPluginAudioPlaybackControl)binding.Source!).AcquirePlayback("ssf");
			Assert.That(source.PlaybackReplaced, Is.True);
			source.Publish(new PluginPcmFrame(new byte[16], PcmEncoding.Float32, 48000, 2, 2, 1));
			Assert.That(count, Is.EqualTo(1)); Assert.Throws<InvalidOperationException>(() => source.AcquirePlayback("other"));
			replacement.Dispose(); Assert.That(source.PlaybackReplaced, Is.False);
		}
		[Test]
		public void InputFiltersHaveDeterministicOrderAndReleaseTheirStage()
		{
			var pipeline = new PluginInputPipeline();
			using var add = pipeline.Register("add", 20, new InputFilter(value => value + 3));
			using var scale = pipeline.Register("scale", 10, new InputFilter(value => value * 2));
			Assert.That(pipeline.Apply(new PluginInputEvent("nudge-x", 1, 10)).Value, Is.EqualTo(5));
			scale.Dispose(); Assert.That(pipeline.Apply(new PluginInputEvent("nudge-x", 1, 10)).Value, Is.EqualTo(4));
		}
		[Test]
		public void SegmentAndAuxiliarySurfaceProvidersUseOnlyVpeContracts()
		{
			var registry = new PluginResourceRegistry(); using var source = new SegmentResourceSource();
			using var publication = registry.Publish(new PluginResourceDescriptor("score", "backglass", "controller", "Score", "score", ResourceKind.Segments, 1, 1), source);
			using var surface = registry.Publish(new PluginResourceDescriptor("backglass", "backglass", "controller", "Backglass", "backglass", ResourceKind.Surface, 1, 1), new Surface());
			using var binding = registry.Bind<IPluginSegmentSource>(new PluginResourceSelection(ResourceKind.Segments) { Session = 1 });
			string layout = ""; using var subscription = binding.Connect(segments => segments.Subscribe(frame => layout = frame.Layout));
			source.Publish(new PluginSegmentFrame("vpe.segment.mask16.v1", 4, 16, new float[64]));
			Assert.That(layout, Is.EqualTo("vpe.segment.mask16.v1")); Assert.That(registry.Snapshot().Count, Is.EqualTo(2));
		}
		[Test]
		public void RequiredCapabilityAmbiguityAndOrderingCyclesAreExplicit()
		{
			var first = new PluginDescriptor("first", "First", "") { Provides = new[] { "cap" } };
			var second = new PluginDescriptor("second", "Second", "") { Provides = new[] { "cap" } };
			var consumer = new PluginDescriptor("consumer", "Consumer", "") { Requires = new[] { "cap" } };
			Assert.Throws<PluginOrderException>(() => PluginOrderResolver.Resolve(new[] { first, second, consumer }, PluginPhase.Start));
			Assert.That(PluginOrderResolver.Resolve(new[] { first, second, consumer }, PluginPhase.Start, preferredProviders: new Dictionary<string, string> { ["cap"] = "second" }).Select(plugin => plugin.Id).ToList().IndexOf("second"), Is.LessThan(2));
			first.DependsOn = new[] { "second" }; second.DependsOn = new[] { "first" };
			Assert.That(Assert.Throws<PluginOrderException>(() => PluginOrderResolver.Resolve(new[] { first, second }, PluginPhase.Start))!.Message, Does.Contain("first"));
		}
		[Test]
		public void ReleaseRunsEveryActionEvenWhenOneFails()
		{
			var scope = new ResourceScope(); var calls = new List<int>();
			scope.Own(new Subscription(() => calls.Add(1))); scope.Own(new Subscription(() => throw new InvalidOperationException())); scope.Own(new Subscription(() => calls.Add(3)));
			Assert.Throws<AggregateException>(scope.Dispose); Assert.That(calls, Is.EqualTo(new[] { 3, 1 })); Assert.DoesNotThrow(scope.Dispose);
		}
		private sealed class InputFilter : IPluginInputFilter
		{
			private readonly Func<float, float> _transform;
			internal InputFilter(Func<float, float> transform) { _transform = transform; }
			public PluginInputEvent Filter(in PluginInputEvent input) => new PluginInputEvent(input.Action, _transform(input.Value), input.TimestampUs);
		}
		private sealed class Surface : IPluginSurface { public ResourceKind Kind => ResourceKind.Surface; public int Width => 1920; public int Height => 1080; public string Backend => "test"; }
	}
}
