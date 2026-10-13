// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// See LICENSE for the terms of use and distribution.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using VisualPinball.Plugins;

namespace VisualPinball.Plugins.Test
{
	[TestFixture]
	public sealed class ResourceTests
	{
		private static PluginResourceDescriptor Display(string id, long instance = 1, string controller = "game", ResourceView view = ResourceView.Original, params string[] parents)
			=> new PluginResourceDescriptor(id, "test", controller, id, "dmd", ResourceKind.Display, 1, instance, view, DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Rgb24, parents);
		private static PluginResourceSelection Selection(ResourceView view = ResourceView.Identification) => new PluginResourceSelection(ResourceKind.Display) { Controller = "game", Session = 1, Intent = view };
		private static PluginDisplayFrame Frame(long instance, DisplayEncoding format = DisplayEncoding.Gray4) => new PluginDisplayFrame(new byte[3 * 2 * (format == DisplayEncoding.Rgb24 ? 3 : 1)], 3, 2, 3 * (format == DisplayEncoding.Rgb24 ? 3 : 1), format, 1, 0, instance);

		[Test]
		public void LatePublicationNegotiatesForEachConsumerAndReleaseDoesNotReleaseAnother()
		{
			int leases = 0;
			var registry = new PluginResourceRegistry();
			using var first = registry.Bind<IPluginDisplaySource>(Selection());
			using var second = registry.Bind<IPluginDisplaySource>(Selection());
			Assert.That(first.State, Is.EqualTo(ResourceBindingState.Waiting));
			using var source = new DisplayResourceSource((view, formats) => { leases++; return new Subscription(() => leases--); });
			using var publication = registry.Publish(Display("raw"), source);
			Assert.That(leases, Is.EqualTo(2));
			first.Dispose(); Assert.That(leases, Is.EqualTo(1));
			second.Dispose(); Assert.That(leases, Is.Zero);
		}
		[Test]
		public void LateParentFromAnotherSessionCannotMakeDerivedResourceReachable()
		{
			var registry = new PluginResourceRegistry();
			Assert.Throws<InvalidOperationException>(() => registry.Publish(Display("colored", view: ResourceView.Presentation, parents: new[] { "raw" }), new DisplayResourceSource()));
			using var raw = registry.Publish(new PluginResourceDescriptor("raw", "test", "game", "raw", "dmd", ResourceKind.Display, 2, 9,
				ResourceView.Original, DisplayEncoding.Gray4), new DisplayResourceSource());
			Assert.That(registry.Snapshot(Selection(ResourceView.Presentation)), Is.Empty);
		}
		[Test]
		public void RuntimeSourceChoicesAreControllerRelativeAndDeferredWhenTheGameIsStopped()
		{
			var game = new GameIdentity("pinmame", "game");
			var idle = PluginResourceChoices.Displays(new PluginChoiceRequest(game, new PluginSettingsSnapshot(1), "windows"), DisplayEncoding.Gray4);
			Assert.That(idle.IsComplete, Is.False);
			string controller = game.Id + "/controller/0";
			var descriptors = new[] {
				new PluginResourceDescriptor(controller, "pinmame", controller, "Controller", "controller", ResourceKind.Controller, 1, 1),
				Display(controller + "/display/dmd0", controller: controller)
			};
			var live = PluginResourceChoices.Displays(new PluginChoiceRequest(game, new PluginSettingsSnapshot(1), "windows", descriptors), DisplayEncoding.Gray4);
			Assert.That(live.IsComplete, Is.True);
			Assert.That(live.Find("display/dmd0"), Is.Not.Null);
			Assert.That(PluginResourceChoices.Resolve(controller, "display/dmd0"), Is.EqualTo(controller + "/display/dmd0"));
			var global = PluginResourceChoices.Displays(new PluginChoiceRequest(null, new PluginSettingsSnapshot(1), "windows", descriptors), DisplayEncoding.Gray4);
			Assert.That(global.Choices.Count, Is.EqualTo(1));
		}
		[Test]
		public void IdentificationConsumersHoldNativePrecisionUntilLastLeaseLeaves()
		{
			var requests = new List<DisplayEncoding>();
			using var negotiation = new DisplayFormatNegotiator(DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8, DisplayEncoding.Luminance8, requests.Add);
			using var first = negotiation.Acquire(ResourceView.Identification, DisplayEncoding.Gray2 | DisplayEncoding.Gray4);
			using var second = negotiation.Acquire(ResourceView.Identification, DisplayEncoding.Gray2 | DisplayEncoding.Gray4);
			first.Dispose();
			Assert.That(requests, Is.EqualTo(new[] { DisplayEncoding.Gray4 }));
			second.Dispose();
			Assert.That(requests, Is.EqualTo(new[] { DisplayEncoding.Gray4, DisplayEncoding.Luminance8 }));
		}
		[Test]
		public void AutoBlocksAmbiguityAndManualMissingNeverFallsBack()
		{
			var registry = new PluginResourceRegistry();
			using var source = new DisplayResourceSource();
			using var raw = registry.Publish(Display("one"), source);
			using var auto = registry.Bind<IPluginDisplaySource>(Selection());
			using var other = registry.Publish(Display("two"), new DisplayResourceSource());
			Assert.That(auto.State, Is.EqualTo(ResourceBindingState.Blocked));
			var explicitChoice = Selection(); explicitChoice.ResourceId = "missing";
			using var manual = registry.Bind<IPluginDisplaySource>(explicitChoice);
			Assert.That(manual.State, Is.EqualTo(ResourceBindingState.Waiting));
			other.Dispose(); Assert.That(auto.State, Is.EqualTo(ResourceBindingState.Bound));
		}
		[Test]
		public void PresentationChoosesDerivedResourceWhileIdentificationPreservesRaw()
		{
			var registry = new PluginResourceRegistry();
			using var raw = registry.Publish(Display("raw"), new DisplayResourceSource());
			using var unrelated = registry.Publish(Display("unrelated", controller: "other"), new DisplayResourceSource());
			using var transformed = registry.Publish(Display("colored", view: ResourceView.Presentation, parents: new[] { "raw" }), new DisplayResourceSource());
			using var identification = registry.Bind<IPluginDisplaySource>(Selection());
			var choice = Selection(ResourceView.Presentation); choice.AllowOriginalFallback = true;
			using var presentation = registry.Bind<IPluginDisplaySource>(choice);
			Assert.That(identification.Descriptor!.Id, Is.EqualTo("raw"));
			Assert.That(presentation.Descriptor!.Id, Is.EqualTo("colored"));
		}
		[Test]
		public void FirstFrameReportsActualDepthAndOldGenerationIsRejected()
		{
			var registry = new PluginResourceRegistry();
			using var binding = registry.Bind<IPluginDisplaySource>(Selection());
			var encodings = new List<DisplayEncoding>();
			using var subscription = binding.SubscribeFrames(frame => encodings.Add(frame.Encoding));
			using var old = new DisplayResourceSource();
			using var first = registry.Publish(Display("raw"), old);
			old.Publish(Frame(1)); Assert.That(binding.ActualEncoding, Is.EqualTo(DisplayEncoding.Gray4));
			first.Dispose();
			using var current = new DisplayResourceSource();
			using var second = registry.Publish(Display("raw", 2), current);
			old.Publish(Frame(1)); current.Publish(Frame(1)); current.Publish(Frame(2, DisplayEncoding.Gray2));
			Assert.That(encodings, Is.EqualTo(new[] { DisplayEncoding.Gray4, DisplayEncoding.Gray2 }));
		}
		[Test]
		public void IncompatibleConcreteFormatDoesNotReachConsumer()
		{
			var registry = new PluginResourceRegistry(); var choice = Selection(); choice.Accepts = DisplayEncoding.Gray2;
			using var source = new DisplayResourceSource(); using var published = registry.Publish(Display("raw"), source);
			using var binding = registry.Bind<IPluginDisplaySource>(choice);
			int count = 0; using var subscription = binding.SubscribeFrames(_ => count++);
			source.Publish(Frame(1)); source.Publish(Frame(1, DisplayEncoding.Gray2));
			Assert.That(count, Is.EqualTo(1));
		}
		[Test]
		public void ThrowingObserverCannotLeakPublication()
		{
			var faults = new List<Exception>(); var registry = new PluginResourceRegistry(faults.Add);
			using var observer = registry.Observe(_ => throw new InvalidOperationException());
			using var source = new DisplayResourceSource(); var lease = registry.Publish(Display("raw"), source);
			lease.Dispose(); Assert.That(registry.Snapshot(Selection()), Is.Empty); Assert.That(faults.Count, Is.EqualTo(2));
		}
		[Test]
		public void OriginalBindingsStayBoundWhenAConsumerPublishesPresentation()
		{
			var registry = new PluginResourceRegistry(); using var raw = registry.Publish(Display("raw"), new DisplayResourceSource());
			using var binding = registry.Bind<IPluginDisplaySource>(Selection(ResourceView.Original));
			using var derived = registry.Publish(Display("colored", view: ResourceView.Presentation, parents: new[] { "raw" }), new DisplayResourceSource());
			Assert.That(binding.State, Is.EqualTo(ResourceBindingState.Bound)); Assert.That(binding.Descriptor!.Id, Is.EqualTo("raw"));
		}
		[Test]
		public void DerivedResourcesCannotSurviveTheirParentGeneration()
		{
			var registry = new PluginResourceRegistry(); var raw = registry.Publish(Display("raw"), new DisplayResourceSource());
			using var derived = registry.Publish(Display("colored", view: ResourceView.Presentation, parents: new[] { "raw" }), new DisplayResourceSource());
			using var binding = registry.Bind<IPluginDisplaySource>(Selection(ResourceView.Presentation));
			Assert.That(binding.State, Is.EqualTo(ResourceBindingState.Bound)); raw.Dispose();
			Assert.That(binding.State, Is.EqualTo(ResourceBindingState.Waiting));
			using var replacement = registry.Publish(Display("raw", 2), new DisplayResourceSource());
			Assert.That(binding.State, Is.EqualTo(ResourceBindingState.Waiting));
		}
		[Test]
		public void PhysicalOutputHasOneOwnerAndCanBeReacquiredAfterCleanup()
		{
			var outputs = new PluginOutputLeases(); using var first = outputs.Acquire("serial:COM3", "one");
			Assert.Throws<InvalidOperationException>(() => outputs.Acquire("serial:COM3", "two"));
			first.Dispose(); using var second = outputs.Acquire("serial:COM3", "two");
			Assert.That(outputs.Owner("serial:COM3"), Is.EqualTo("two"));
		}
	}
}
