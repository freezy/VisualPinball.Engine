// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using Unity.Collections;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Side list the physical rubber generator writes its compliant span records into
	/// while colliders are created.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>RubberApi.CreateColliders</c> is also called by the collider gizmo, which has
	/// no physics state to register into, so the generator never registers anything
	/// itself. It stamps each eligible capsule with the span's index inside its own
	/// rubber and appends the record here; <see cref="PhysicsEngine"/> turns those
	/// per-rubber indices into dense runtime ids once every collider exists.
	/// </para>
	/// <para>
	/// Managed and main-thread only: it is gone by the time the physics loop runs.
	/// </para>
	/// </remarks>
	/// <summary>
	/// The main-thread mapping between a dense span id and the rubber span it was
	/// assigned to, built once by <see cref="RubberSpanReboundCollector.Register"/>.
	/// </summary>
	/// <remarks>
	/// The collide pass only ever knows the dense id, so everything on the main thread
	/// that has to go the other way — the slingshot api resolving its bound span, a
	/// renderer selecting the span an impact event names — reads it here. Immutable
	/// after <see cref="PhysicsEngine.Start"/>.
	/// </remarks>
	internal sealed class RubberSpanReboundIndex
	{
		internal static readonly RubberSpanReboundIndex Empty = new(new List<RubberSpanReboundCollector.Entry>());

		private readonly int[] _itemIds;
		private readonly int[] _pathElementIndices;
		private readonly Dictionary<long, int> _spanIds;

		internal int Count => _itemIds.Length;

		internal RubberSpanReboundIndex(IReadOnlyList<RubberSpanReboundCollector.Entry> ordered)
		{
			_itemIds = new int[ordered.Count];
			_pathElementIndices = new int[ordered.Count];
			_spanIds = new Dictionary<long, int>(ordered.Count);
			for (var i = 0; i < ordered.Count; i++) {
				_itemIds[i] = ordered[i].ItemId;
				_pathElementIndices[i] = ordered[i].Record.PathElementIndex;
				_spanIds[Key(_itemIds[i], _pathElementIndices[i])] = i;
			}
		}

		/// <summary>The dense span id of a rubber's baked free span, if it got one.</summary>
		internal bool TryGetSpanId(int itemId, int pathElementIndex, out int spanId)
			=> _spanIds.TryGetValue(Key(itemId, pathElementIndex), out spanId);

		/// <summary>The rubber and path element a dense span id stands for.</summary>
		internal bool TryGetSpan(int spanId, out int itemId, out int pathElementIndex)
		{
			if (spanId < 0 || spanId >= _itemIds.Length) {
				itemId = 0;
				pathElementIndex = -1;
				return false;
			}
			itemId = _itemIds[spanId];
			pathElementIndex = _pathElementIndices[spanId];
			return true;
		}

		private static long Key(int itemId, int pathElementIndex)
			=> ((long)itemId << 32) | (uint)pathElementIndex;
	}

	internal sealed class RubberSpanReboundCollector
	{
		internal struct Entry
		{
			/// <summary>Item id of the rubber that emitted the span.</summary>
			internal int ItemId;

			/// <summary>Index of the span within its own rubber, as stamped on the capsules.</summary>
			internal int LocalIndex;

			internal RubberSpanRebound Record;
		}

		private readonly List<Entry> _entries = new();

		internal int Count => _entries.Count;

		internal IReadOnlyList<Entry> Entries => _entries;

		internal void Add(int itemId, int localIndex, in RubberSpanRebound record)
			=> _entries.Add(new Entry { ItemId = itemId, LocalIndex = localIndex, Record = record });

		/// <summary>
		/// Assigns the dense span ids, stamps them onto the emitted capsules and fills
		/// the runtime map. Span ids are dense and assigned in registration order:
		/// rubbers sorted by item id, spans by path element index.
		/// </summary>
		/// <remarks>
		/// Every swept-circle capsule whose <c>SpanId</c> does not resolve is reset to
		/// <c>-1</c>, so a capsule can never point at a foreign span.
		/// </remarks>
		/// <returns>
		/// The dense-id mapping the main thread needs afterwards, see
		/// <see cref="RubberSpanReboundIndex"/>.
		/// </returns>
		internal RubberSpanReboundIndex Register(ref ColliderReference colliders,
			ref ColliderReference kinematicColliders,
			ref NativeParallelHashMap<int, RubberSpanRebound> spans)
		{
			spans.Clear();
			var ordered = new List<Entry>(_entries);
			ordered.Sort((left, right) => {
				var byItem = left.ItemId.CompareTo(right.ItemId);
				return byItem != 0
					? byItem
					: left.Record.PathElementIndex.CompareTo(right.Record.PathElementIndex);
			});

			var ids = new Dictionary<long, int>(ordered.Count);
			if (spans.Capacity < ordered.Count) {
				spans.Capacity = ordered.Count;
			}
			for (var i = 0; i < ordered.Count; i++) {
				var entry = ordered[i];
				ids[Key(entry.ItemId, entry.LocalIndex)] = i;
				spans.Add(i, entry.Record);
			}

			Stamp(ref colliders, ids);
			Stamp(ref kinematicColliders, ids);
			return new RubberSpanReboundIndex(ordered);
		}

		private static void Stamp(ref ColliderReference colliders, Dictionary<long, int> ids)
		{
			for (var i = 0; i < colliders.SweptCircleColliders.Length; i++) {
				ref var collider = ref colliders.SweptCircleColliders.GetElementAsRef(i);
				if (collider.SpanId < 0) {
					continue;
				}
				collider.SpanId = ids.TryGetValue(Key(collider.Header.ItemId, collider.SpanId),
					out var spanId)
					? spanId
					: -1;
			}
		}

		private static long Key(int itemId, int localIndex)
			=> ((long)itemId << 32) | (uint)localIndex;
	}
}
