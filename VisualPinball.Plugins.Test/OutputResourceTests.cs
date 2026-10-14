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
		public void AChosenDisplayIsPresentedThroughItsDerivedView()
		{
			var registry = new PluginResourceRegistry();
			const string original = "pinmame/controller/0/display/dmd0", presentation = original + "/presentation/org.visualpinball.dmd";
			const DisplayEncoding formats = DisplayEncoding.Gray4 | DisplayEncoding.Rgb24;
			using var originalSource = new DisplayResourceSource();
			using var originalPublication = registry.Publish(new PluginResourceDescriptor(original, "pinmame", "pinmame/controller/0", "dmd0", "dmd", ResourceKind.Display, 1, 1, ResourceView.Original, formats), originalSource);
			var selection = new PluginResourceSelection(ResourceKind.Display) { ResourceId = original, Session = 1, Intent = ResourceView.Presentation, AllowOriginalFallback = true };
			using var binding = registry.Bind<IPluginDisplaySource>(selection);
			// Without a colorizer the chosen display itself is used.
			Assert.That(binding.Descriptor!.Id, Is.EqualTo(original));
			// Once a plugin presents it, the chosen display is shown through that view.
			using var presentationSource = new DisplayResourceSource();
			using (registry.Publish(new PluginResourceDescriptor(presentation, "org.visualpinball.dmd", "pinmame/controller/0", "dmd0 colorized", "dmd", ResourceKind.Display, 1, 2, ResourceView.Presentation, formats, original), presentationSource)) {
				Assert.That(binding.Descriptor!.Id, Is.EqualTo(presentation));
			}
			Assert.That(binding.Descriptor!.Id, Is.EqualTo(original));
		}

		[Test]
		public void AChosenDisplayIgnoresOtherDisplaysPresentations()
		{
			var registry = new PluginResourceRegistry();
			const string chosen = "pinmame/controller/0/display/dmd0", other = "pinmame/controller/0/display/dmd1";
			const DisplayEncoding formats = DisplayEncoding.Gray4 | DisplayEncoding.Rgb24;
			using var chosenSource = new DisplayResourceSource();
			using var otherSource = new DisplayResourceSource();
			using var presentationSource = new DisplayResourceSource();
			using var chosenPublication = registry.Publish(new PluginResourceDescriptor(chosen, "pinmame", "pinmame/controller/0", "dmd0", "dmd", ResourceKind.Display, 1, 1, ResourceView.Original, formats), chosenSource);
			using var otherPublication = registry.Publish(new PluginResourceDescriptor(other, "pinmame", "pinmame/controller/0", "dmd1", "dmd", ResourceKind.Display, 1, 2, ResourceView.Original, formats), otherSource);
			using var presentationPublication = registry.Publish(new PluginResourceDescriptor(other + "/presentation/x", "x", "pinmame/controller/0", "dmd1 colorized", "dmd", ResourceKind.Display, 1, 3, ResourceView.Presentation, formats, other), presentationSource);
			using var binding = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = chosen, Session = 1, Intent = ResourceView.Presentation, AllowOriginalFallback = true });
			Assert.That(binding.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(binding.Descriptor!.Id, Is.EqualTo(chosen));
		}

		[Test]
		public void ControllerSourcesCarryOnlyControllerKinds()
		{
			Assert.Throws<System.ArgumentException>(() => new ControllerResourceSource(ResourceKind.Display));
		}
	}
}
