// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Implemented by a gamelogic engine that knows which game it runs, so the Player can tell plugins which game is being
	/// played without opening the table package.
	/// </summary>
	/// <remarks>
	/// The identity selects per-game plugin settings and lets plugins find matching media, such as a PinUP pack or a
	/// backglass for the ROM. The Player asks the loaded table's engine first and only falls back to reading the package
	/// (see <see cref="IPackagedGameIdentityReader"/>) when the engine cannot answer.
	/// <example>
	/// A ROM-based engine reports its ROM name:
	/// <code>
	/// public class MyRomEngine : MonoBehaviour, IGamelogicEngine, IGameIdentityProvider
	/// {
	///     public string romId = "t2_l8";
	///     GameIdentity? IGameIdentityProvider.GameIdentity =&gt; string.IsNullOrWhiteSpace(romId) ? null : new GameIdentity("myrom", romId);
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IGameIdentityProvider
	{
		/// <summary>
		/// The game this engine is configured to run, or null when it cannot tell yet (for example before a ROM is
		/// selected). Must be cheap and side-effect free: the Player reads it while preparing a game, before the engine starts.
		/// </summary>
		GameIdentity? GameIdentity { get; }
	}

	/// <summary>
	/// Reads a game identity from one component's serialized data inside a table package, without loading the table.
	/// </summary>
	/// <remarks>
	/// The Player uses readers to identify a game from the package alone, for instance to show per-game plugin settings in
	/// the table browser before anything is loaded. Each reader handles one component type; the Player walks the package's
	/// components, hands every payload of a matching type to its reader, and uses the single identity found. Without any
	/// identity the package's content hash identifies the game.
	/// <para>
	/// A reader must only parse the payload. It must not initialize the component's native runtime (no emulator, no
	/// device), since it runs on a worker thread for packages that may never be played.
	/// </para>
	/// <example>
	/// The PinMAME integration reads the ROM name from its engine component:
	/// <code>
	/// internal sealed class PinMamePackageIdentityReader : IPackagedGameIdentityReader
	/// {
	///     public string ComponentType =&gt; "PinMameGamelogicEngine";
	///     public GameIdentity? Read(ReadOnlyMemory&lt;byte&gt; payload)
	///     {
	///         var values = JObject.Parse(Encoding.UTF8.GetString(payload.Span));
	///         var rom = values.Value&lt;string&gt;("RomId");
	///         return string.IsNullOrWhiteSpace(rom) ? null : new GameIdentity("pinmame", rom);
	///     }
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IPackagedGameIdentityReader
	{
		/// <summary>
		/// Name of the component type whose payloads this reader understands, as stored in the package (the component's
		/// type name, such as <c>PinMameGamelogicEngine</c>). Used as the registry key, so it must be unique.
		/// </summary>
		string ComponentType { get; }

		/// <summary>
		/// Extracts the game identity from one serialized component payload, or returns null when the payload does not name
		/// a game (for example an engine with no ROM selected). Called on a worker thread; must not touch Unity.
		/// </summary>
		/// <param name="payload">The component's raw serialized data, borrowed for the duration of the call.</param>
		GameIdentity? Read(ReadOnlyMemory<byte> payload);
	}

	/// <summary>
	/// Registry of <see cref="IPackagedGameIdentityReader"/>s, keyed by component type, that the Player consults when it
	/// identifies a table package.
	/// </summary>
	/// <remarks>
	/// Integration packages register their reader once at startup, typically from a
	/// <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]</c> method. The registry is not
	/// thread-safe: register on the main thread before packages are read.
	/// </remarks>
	public static class PackagedGameIdentityReaders
	{
		private static readonly Dictionary<string, IPackagedGameIdentityReader> Readers = new Dictionary<string, IPackagedGameIdentityReader>(StringComparer.Ordinal);

		/// <summary>
		/// Removes all registered readers. The host calls this when the scripting domain starts, so readers registered by a
		/// previous play session in the editor do not linger; plugins have no reason to call it.
		/// </summary>
		public static void Reset() => Readers.Clear();

		/// <summary>Adds a reader for its <see cref="IPackagedGameIdentityReader.ComponentType"/>.</summary>
		/// <remarks>
		/// <example>
		/// <code>
		/// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		/// private static void Register() =&gt; PackagedGameIdentityReaders.Register(new PinMamePackageIdentityReader());
		/// </code>
		/// </example>
		/// </remarks>
		/// <exception cref="ArgumentException">A reader for the same component type is already registered.</exception>
		public static void Register(IPackagedGameIdentityReader reader) => Readers.Add(reader.ComponentType, reader);

		/// <summary>
		/// Returns the reader for a component type, or null when no integration understands that component. The Player calls
		/// this for every component in a package and skips the ones without a reader.
		/// </summary>
		/// <param name="componentType">The component type name as stored in the package.</param>
		public static IPackagedGameIdentityReader? Find(string componentType) => Readers.TryGetValue(componentType, out var reader) ? reader : null;
	}
}
