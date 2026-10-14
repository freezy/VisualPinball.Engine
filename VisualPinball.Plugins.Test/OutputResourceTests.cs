// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using NUnit.Framework;
using VisualPinball.Plugins;

namespace VisualPinball.Plugins.Test
{
	[TestFixture]
	public sealed class OutputResourceTests
	{
		[Test]
		public void NativeOutputsLeaveRolelessControllerBindingsUnambiguous()
		{
			var registry = new PluginResourceRegistry();
			using var mapped = new ControllerResourceSource();
			using var native = new ControllerResourceSource(ResourceKind.Outputs);
			using var mappedPublication = registry.Publish(new PluginResourceDescriptor("pinmame", "pinmame", "pinmame", "PinMAME", "controller", ResourceKind.Controller, 1, 1), mapped);
			using var nativePublication = registry.Publish(new PluginResourceDescriptor("pinmame/outputs", "pinmame", "pinmame", "PinMAME outputs", "outputs", ResourceKind.Outputs, 1, 2), native);
			using var controller = registry.Bind<IPluginControllerSource>(new PluginResourceSelection(ResourceKind.Controller) { Controller = "pinmame", Session = 1 });
			using var outputs = registry.Bind<IPluginControllerSource>(new PluginResourceSelection(ResourceKind.Outputs) { Controller = "pinmame", Session = 1 });
			Assert.That(controller.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(controller.Source, Is.SameAs(mapped));
			Assert.That(outputs.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(outputs.Source, Is.SameAs(native));
			int number = 0;
			using var subscription = outputs.Connect(source => source.Subscribe(change => number = change.Number));
			native.Publish(new PluginControllerEvent(ControllerEventKind.Lamp, "81", 128, 1, 0, 81, ControllerValueSemantics.Physical, 255));
			Assert.That(number, Is.EqualTo(81));
		}

		[Test]
		public void SegmentDisplaysAnnounceTheirPlaceBeforeTheFirstFrame()
		{
			var registry = new PluginResourceRegistry();
			using var source = new SegmentResourceSource(1, 7, "seg14", "vfd-green");
			using var publication = registry.Publish(new PluginResourceDescriptor("pinmame/segments/display1", "pinmame", "pinmame", "display1", "segments", ResourceKind.Segments, 1, 1), source);
			using var binding = registry.Bind<IPluginSegmentSource>(new PluginResourceSelection(ResourceKind.Segments) { Session = 1 });
			Assert.That(binding.Source!.Order, Is.EqualTo(1));
			Assert.That(binding.Source.Elements, Is.EqualTo(7));
			Assert.That(binding.Source.Layout, Is.EqualTo("seg14"));
			Assert.That(binding.Source.Hardware, Is.EqualTo("vfd-green"));
		}

		[Test]
		public void ControllerSourcesCarryOnlyControllerKinds()
		{
			Assert.Throws<System.ArgumentException>(() => new ControllerResourceSource(ResourceKind.Display));
		}
	}
}
