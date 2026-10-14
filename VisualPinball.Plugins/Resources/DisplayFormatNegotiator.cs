// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Decides which encoding a display producer delivers when consumers need raw frames for identification. Provider-group
	/// raw mode is held until the last identification consumer leaves.
	/// </summary>
	/// <remarks>
	/// A producer normally delivers a processed default encoding (PinMAME: <see cref="DisplayEncoding.Luminance8"/>).
	/// Consumers that recognize content, such as PinUP's DMD triggers or the DMD plugin's colorization, need the
	/// controller's raw <see cref="DisplayEncoding.Gray4"/> or <see cref="DisplayEncoding.Gray2"/> shades instead. When the
	/// first of them acquires an identification view, the negotiator asks the producer for raw frames (Gray4 when allowed,
	/// otherwise Gray2); later consumers join if they accept that encoding; when the last one leaves, it asks for the
	/// default again. Consumers of other views get no say, so while raw mode is held they receive raw frames too.
	/// <para>
	/// VPE creates one per display, or a single one for all displays when the engine switches all of them at once (PinMAME
	/// does), and passes its <see cref="Acquire"/> method to <see cref="DisplayResourceSource"/> as the view negotiation.
	/// It is not thread-safe: acquire and release leases on the owner thread, as bindings do. It assumes the producer starts
	/// in the default encoding and does not request it on creation.
	/// </para>
	/// <example>
	/// <code>
	/// var negotiator = new DisplayFormatNegotiator(DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8,
	///     DisplayEncoding.Luminance8, encoding =&gt; engine.RequestFrameFormat(encoding),
	///     () =&gt; registry.NotifyChanged(displayResourceId));
	/// var source = new DisplayResourceSource(negotiator.Acquire, Debug.LogException);
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class DisplayFormatNegotiator : IDisposable
	{
		private readonly DisplayEncoding _default;
		private readonly DisplayEncoding _supported;
		private readonly Action<DisplayEncoding> _request;
		private readonly Action? _released;
		private int _identification;
		private DisplayEncoding _active;
		private bool _disposed;
		/// <summary>Creates a negotiator for one producer or group of displays.</summary>
		/// <param name="supported">Every encoding the producer can deliver, as flags.</param>
		/// <param name="defaultFormat">The single encoding the producer delivers when no identification consumer is attached.</param>
		/// <param name="request">
		/// Asks the producer to switch to one encoding. Called synchronously from <see cref="Acquire"/>, from disposing a
		/// lease and from <see cref="Dispose"/>, on the caller's thread.
		/// </param>
		/// <param name="released">
		/// Optional. Called after the last identification lease was disposed and the default was requested, so consumers
		/// that were refused because of the raw encoding just given up can try again. VPE passes a callback that calls
		/// <see cref="PluginResourceRegistry.NotifyChanged"/> for the affected displays. Not called by <see cref="Dispose"/>.
		/// </param>
		public DisplayFormatNegotiator(DisplayEncoding supported, DisplayEncoding defaultFormat, Action<DisplayEncoding> request, Action? released = null)
		{ _supported = supported; _default = defaultFormat; _request = request; _released = released; }
		/// <summary>
		/// Registers a consumer's view. Only <see cref="ResourceView.Identification"/> changes anything: the first such
		/// consumer switches the producer to raw <see cref="DisplayEncoding.Gray4"/> (or <see cref="DisplayEncoding.Gray2"/>
		/// when Gray4 is not both supported and accepted), and further ones join that encoding. Other views get a lease that
		/// does nothing. Matches <see cref="IPluginDisplaySource.AcquireView"/>.
		/// </summary>
		/// <param name="intent">The view the consumer wants.</param>
		/// <param name="accepted">The encodings the consumer can read.</param>
		/// <returns>
		/// A lease. Disposing the last identification lease switches the producer back to the default encoding and then
		/// calls the <c>released</c> callback; disposing after the negotiator was disposed does nothing.
		/// </returns>
		/// <exception cref="ObjectDisposedException">The negotiator was disposed.</exception>
		/// <exception cref="InvalidOperationException">
		/// The producer cannot deliver a raw encoding the consumer accepts, or raw frames are already delivered in an encoding
		/// this consumer does not accept.
		/// </exception>
		public IDisposable Acquire(ResourceView intent, DisplayEncoding accepted)
		{
			if (_disposed) throw new ObjectDisposedException(nameof(DisplayFormatNegotiator));
			if (intent != ResourceView.Identification) return new Subscription(() => { });
			var raw = _supported & accepted & (DisplayEncoding.Gray2 | DisplayEncoding.Gray4);
			if (raw == DisplayEncoding.None) throw new InvalidOperationException("This source cannot provide unmodified identification frames.");
			if (_identification > 0) {
				// The provider group delivers one raw encoding at a time; a consumer that cannot read it cannot join.
				if ((raw & _active) == DisplayEncoding.None) throw new InvalidOperationException("Identification frames are already delivered as " + _active + ", which this consumer does not accept.");
				_identification++;
			} else {
				var encoding = (raw & DisplayEncoding.Gray4) != 0 ? DisplayEncoding.Gray4 : DisplayEncoding.Gray2;
				_request(encoding);
				_active = encoding;
				_identification = 1;
			}
			return new Subscription(() => {
				if (_disposed || --_identification != 0) return;
				_active = DisplayEncoding.None;
				_request(_default);
				// Consumers that were refused because of the encoding we just gave up can try again.
				_released?.Invoke();
			});
		}
		/// <summary>
		/// Stops negotiating. If identification consumers were still attached, the producer is switched back to the default
		/// encoding; leases disposed afterwards do nothing. Calling it again has no effect.
		/// </summary>
		public void Dispose() { if (_disposed) return; _disposed = true; if (_identification > 0) _request(_default); _identification = 0; }
	}
}
