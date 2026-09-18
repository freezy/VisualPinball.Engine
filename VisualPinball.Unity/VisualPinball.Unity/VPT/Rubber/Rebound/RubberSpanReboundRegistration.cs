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
		internal void Register(ref ColliderReference colliders,
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
