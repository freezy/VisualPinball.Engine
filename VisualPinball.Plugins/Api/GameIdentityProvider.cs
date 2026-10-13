// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// See LICENSE for the terms of use and distribution.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	public interface IGameIdentityProvider { GameIdentity? GameIdentity { get; } }
	/// <summary>Reads only the provider's inert package payload; it must not initialize its native runtime.</summary>
	public interface IPackagedGameIdentityReader
	{
		string ComponentType { get; }
		GameIdentity? Read(ReadOnlyMemory<byte> payload);
	}
	public static class PackagedGameIdentityReaders
	{
		private static readonly Dictionary<string, IPackagedGameIdentityReader> Readers = new Dictionary<string, IPackagedGameIdentityReader>(StringComparer.Ordinal);
		public static void Reset() => Readers.Clear();
		public static void Register(IPackagedGameIdentityReader reader) => Readers.Add(reader.ComponentType, reader);
		public static IPackagedGameIdentityReader? Find(string componentType) => Readers.TryGetValue(componentType, out var reader) ? reader : null;
	}
}
