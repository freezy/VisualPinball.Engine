// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using VisualPinball.Plugins;

namespace VisualPinball.Plugins.Test
{
	[TestFixture]
	public sealed class SettingsAndNegotiationTests
	{
		private string _directory = null!;
		private readonly PluginDescriptor _plugin = new PluginDescriptor("test.apply", "Apply", "Test");

		[SetUp] public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "vpe-plugin-tests", Guid.NewGuid().ToString("N"));
		[TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

		[Test]
		public async Task EditsMadeWhileApplyingStayPending()
		{
			var schema = new PluginSettingsSchema(1, new PluginSettingsGroup("General", "",
				new PluginField("a", "A", SettingKind.Text) { Default = "" },
				new PluginField("b", "B", SettingKind.Text) { Default = "" }));
			var store = new PluginConfigurationStore(_directory);
			using var session = new PluginSettingsSession(new Factory(_plugin, schema), store, "windows");
			session.Set("a", "one");
			// The draft is frozen synchronously when Apply starts; these edits come after.
			var apply = session.ApplyAsync();
			session.Set("a", "two");
			session.Set("b", "later");
			Assert.That((await apply).Success, Is.True);

			var saved = store.Read(_plugin, schema);
			Assert.That(saved.String("a"), Is.EqualTo("one"));
			Assert.That(saved.String("b"), Is.EqualTo(""));
			Assert.That(session.Draft.String("a"), Is.EqualTo("two"));
			Assert.That(session.Draft.String("b"), Is.EqualTo("later"));
			Assert.That(session.HasEdits("a"), Is.True);
			Assert.That(session.HasEdits("b"), Is.True);
		}

		[Test]
		public async Task UnchangedFieldsFollowTheSavedProfileAfterApply()
		{
			var schema = new PluginSettingsSchema(1, new PluginSettingsGroup("General", "",
				new PluginField("a", "A", SettingKind.Text) { Default = "" }));
			var store = new PluginConfigurationStore(_directory);
			using var session = new PluginSettingsSession(new Factory(_plugin, schema), store, "windows");
			session.Set("a", "one");
			Assert.That((await session.ApplyAsync()).Success, Is.True);
			Assert.That(session.Draft.String("a"), Is.EqualTo("one"));
			Assert.That(session.HasEdits("a"), Is.False);
		}

		[Test]
		public void IdentificationEncodingIsOneTheConsumerAccepts()
		{
			var requested = new List<DisplayEncoding>();
			using var negotiator = new DisplayFormatNegotiator(DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8, DisplayEncoding.Luminance8, requested.Add);
			var gray2Only = negotiator.Acquire(ResourceView.Identification, DisplayEncoding.Gray2);
			Assert.That(requested, Is.EqualTo(new[] { DisplayEncoding.Gray2 }));
			// A second consumer must be able to read the encoding already being delivered.
			Assert.Throws<InvalidOperationException>(() => negotiator.Acquire(ResourceView.Identification, DisplayEncoding.Gray4));
			var both = negotiator.Acquire(ResourceView.Identification, DisplayEncoding.Gray2 | DisplayEncoding.Gray4);
			Assert.That(requested, Has.Count.EqualTo(1));
			gray2Only.Dispose();
			both.Dispose();
			Assert.That(requested[requested.Count - 1], Is.EqualTo(DisplayEncoding.Luminance8));
			// With the group idle again, a Gray4 consumer gets Gray4.
			using var gray4 = negotiator.Acquire(ResourceView.Identification, DisplayEncoding.Gray4);
			Assert.That(requested[requested.Count - 1], Is.EqualTo(DisplayEncoding.Gray4));
		}

		[Test]
		public void RefusedDisplayBindingRetriesWhenTheConflictingLeaseEnds()
		{
			var registry = new PluginResourceRegistry();
			const string id = "pinmame/controller/0/display/dmd0";
			using var negotiator = new DisplayFormatNegotiator(DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8, DisplayEncoding.Luminance8,
				_ => { }, () => registry.NotifyChanged(id));
			using var source = new DisplayResourceSource(negotiator.Acquire);
			using var publication = registry.Publish(new PluginResourceDescriptor(id, "pinmame", "pinmame/controller/0", "dmd0", "dmd", ResourceKind.Display, 1, 1, ResourceView.Original, DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8), source);
			var gray2 = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = id, Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray2 });
			using var gray4 = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = id, Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray4 });
			Assert.That(gray2.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(gray4.State, Is.EqualTo(ResourceBindingState.Blocked));
			// Stopping the first integration frees the encoding without restarting the game.
			gray2.Dispose();
			Assert.That(gray4.State, Is.EqualTo(ResourceBindingState.Bound));
		}

		[Test]
		public void AFailingRetryStaysBlockedInsteadOfLooping()
		{
			var registry = new PluginResourceRegistry();
			const string id = "pinmame/controller/0/display/dmd0";
			using var negotiator = new DisplayFormatNegotiator(DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8, DisplayEncoding.Luminance8,
				_ => { }, () => registry.NotifyChanged(id));
			using var source = new DisplayResourceSource(negotiator.Acquire);
			using var publication = registry.Publish(new PluginResourceDescriptor(id, "pinmame", "pinmame/controller/0", "dmd0", "dmd", ResourceKind.Display, 1, 1, ResourceView.Original, DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8), source);
			var gray2 = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = id, Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray2 });
			using var gray4 = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = id, Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray4 });
			int attempts = 0;
			using var connection = gray4.Connect(_ => { attempts++; throw new InvalidOperationException("receiver failed"); });
			// The retry acquires the lease, the receiver throws, and the lease rollback must not trigger the same retry again.
			Assert.DoesNotThrow(() => gray2.Dispose());
			Assert.That(attempts, Is.LessThanOrEqualTo(2));
			Assert.That(gray4.State, Is.EqualTo(ResourceBindingState.Blocked));
		}

		[Test]
		public void AFailingRetryOnASharedLeaseStaysBlockedInsteadOfLooping()
		{
			var registry = new PluginResourceRegistry();
			const string first = "pinmame/controller/0/display/dmd0", second = "pinmame/controller/0/display/dmd1";
			const DisplayEncoding formats = DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8;
			// One negotiator for the whole engine notifies every display when its lease ends, as GamelogicPluginResources does.
			using var negotiator = new DisplayFormatNegotiator(formats, DisplayEncoding.Luminance8, _ => { }, () => { registry.NotifyChanged(first); registry.NotifyChanged(second); });
			using var firstSource = new DisplayResourceSource(negotiator.Acquire);
			using var secondSource = new DisplayResourceSource(negotiator.Acquire);
			using var firstPublication = registry.Publish(new PluginResourceDescriptor(first, "pinmame", "pinmame/controller/0", "dmd0", "dmd", ResourceKind.Display, 1, 1, ResourceView.Original, formats), firstSource);
			using var secondPublication = registry.Publish(new PluginResourceDescriptor(second, "pinmame", "pinmame/controller/0", "dmd1", "dmd", ResourceKind.Display, 1, 2, ResourceView.Original, formats), secondSource);
			var gray2 = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = first, Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray2 });
			using var gray4 = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = first, Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray4 });
			int attempts = 0;
			using var connection = gray4.Connect(_ => { attempts++; throw new InvalidOperationException("receiver failed"); });
			Assert.DoesNotThrow(() => gray2.Dispose());
			Assert.That(attempts, Is.LessThanOrEqualTo(2));
			Assert.That(gray4.State, Is.EqualTo(ResourceBindingState.Blocked));
		}

		[Test]
		public void DisposingABindingFromAChangeHandlerIsFinal()
		{
			var registry = new PluginResourceRegistry();
			const string id = "pinmame/controller/0/display/dmd0";
			using var negotiator = new DisplayFormatNegotiator(DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8, DisplayEncoding.Luminance8,
				_ => { }, () => registry.NotifyChanged(id));
			using var source = new DisplayResourceSource(negotiator.Acquire);
			using var publication = registry.Publish(new PluginResourceDescriptor(id, "pinmame", "pinmame/controller/0", "dmd0", "dmd", ResourceKind.Display, 1, 1, ResourceView.Original, DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8), source);
			var gray2 = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = id, Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray2 });
			var gray4 = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = id, Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray4 });
			gray4.Changed += () => gray4.Dispose();
			gray2.Dispose();
			Assert.That(gray4.State, Is.EqualTo(ResourceBindingState.Disposed));
		}

		[Test]
		public void ChangesDuringAReleaseAreNotLost()
		{
			var registry = new PluginResourceRegistry();
			const string first = "pinmame/controller/0/display/dmd0", second = "pinmame/controller/0/display/dmd1", third = "pinmame/controller/0/display/dmd2";
			const DisplayEncoding formats = DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8;
			using var negotiator = new DisplayFormatNegotiator(formats, DisplayEncoding.Luminance8, _ => { }, () => registry.NotifyChanged(first));
			using var source = new DisplayResourceSource(negotiator.Acquire);
			using var publication = registry.Publish(new PluginResourceDescriptor(first, "pinmame", "pinmame/controller/0", "dmd0", "dmd", ResourceKind.Display, 1, 1, ResourceView.Original, formats), source);
			// Bound to the only display, holding Gray2.
			using var any = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray2 });
			// Refused on the same display until Gray2 is released; once it gets in, it publishes a third display.
			using var thirdSource = new DisplayResourceSource();
			IDisposable? thirdPublication = null;
			using var gray4 = registry.Bind<IPluginDisplaySource>(new PluginResourceSelection(ResourceKind.Display) { ResourceId = first, Session = 1, Intent = ResourceView.Identification, Accepts = DisplayEncoding.Gray4 });
			gray4.Changed += () => {
				if (gray4.State == ResourceBindingState.Bound && thirdPublication == null)
					thirdPublication = registry.Publish(new PluginResourceDescriptor(third, "pinmame", "pinmame/controller/0", "dmd2", "dmd", ResourceKind.Display, 1, 3, ResourceView.Original, formats), thirdSource);
			};
			Assert.That(any.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(gray4.State, Is.EqualTo(ResourceBindingState.Blocked));
			// A second display makes the first binding ambiguous. Releasing its view lets the Gray4 binding in, which publishes a
			// third display while the first binding is still releasing; that publication must not be lost.
			using var secondSource = new DisplayResourceSource();
			using var secondPublication = registry.Publish(new PluginResourceDescriptor(second, "pinmame", "pinmame/controller/0", "dmd1", "dmd", ResourceKind.Display, 1, 2, ResourceView.Original, formats), secondSource);
			Assert.That(gray4.State, Is.EqualTo(ResourceBindingState.Bound));
			Assert.That(thirdPublication, Is.Not.Null);
			Assert.That(any.Diagnostic, Does.Contain(third));
			thirdPublication!.Dispose();
		}

		private sealed class Factory : IPlayerPluginFactory
		{
			public PluginDescriptor Descriptor { get; }
			public PluginSettingsSchema Schema { get; }
			public IPluginSettingsProvider? SettingsProvider => null;
			internal Factory(PluginDescriptor descriptor, PluginSettingsSchema schema) { Descriptor = descriptor; Schema = schema; }
			public IPlayerPlugin Create(PluginApplicationContext context) => throw new NotSupportedException();
		}
	}
}
