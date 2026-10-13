// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;

namespace VisualPinball.Plugins
{
	/// <summary>Provider-group raw mode is held until the last identification consumer leaves.</summary>
	public sealed class DisplayFormatNegotiator : IDisposable
	{
		private readonly DisplayEncoding _default;
		private readonly DisplayEncoding _supported;
		private readonly Action<DisplayEncoding> _request;
		private int _identification;
		private bool _disposed;
		public DisplayFormatNegotiator(DisplayEncoding supported, DisplayEncoding defaultFormat, Action<DisplayEncoding> request)
		{ _supported = supported; _default = defaultFormat; _request = request; }
		public IDisposable Acquire(ResourceView intent, DisplayEncoding accepted)
		{
			if (_disposed) throw new ObjectDisposedException(nameof(DisplayFormatNegotiator));
			if (intent != ResourceView.Identification) return new Subscription(() => { });
			var raw = _supported & accepted & (DisplayEncoding.Gray2 | DisplayEncoding.Gray4);
			if (raw == DisplayEncoding.None) throw new InvalidOperationException("This source cannot provide unmodified identification frames.");
			if (_identification++ == 0) {
				try { _request((_supported & DisplayEncoding.Gray4) != 0 ? DisplayEncoding.Gray4 : DisplayEncoding.Gray2); }
				catch { _identification--; throw; }
			}
			return new Subscription(() => { if (!_disposed && --_identification == 0) _request(_default); });
		}
		public void Dispose() { if (_disposed) return; _disposed = true; if (_identification > 0) _request(_default); _identification = 0; }
	}
}
