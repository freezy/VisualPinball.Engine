using System;
using Unity.Collections;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct KdRoot : IDisposable
	{
		public int NumNodes;                                                   // m_num_nodes
		public NativeArray<int> OrgIdx;                                        // m_org_idx
		public NativeArray<int> Indices;                                       // tmp

		private KdNode _rootNode;                                              // m_rootNode
		private NativeArray<Aabb> _bounds;                                     // m_org_vho
		private NativeArray<KdNode> _nodes;                                    // m_nodes

		private int _numItems;                                                 // m_num_items

		public void Init(NativeArray<Aabb> bounds, Allocator allocator)
		{
			_bounds = bounds;
			_numItems = bounds.Length;

			OrgIdx = new NativeArray<int>(_numItems, allocator);
			Indices = new NativeArray<int>(_numItems, allocator);
			_nodes = new NativeArray<KdNode>(_numItems * 2 + 1, allocator);

			NumNodes = 0;
			_rootNode = new KdNode();
			_rootNode.Reset();

			FillFromVector(bounds);
		}

		private void FillFromVector(NativeArray<Aabb> bounds)
		{
			_rootNode.Bounds.Clear();
			_rootNode.Start = 0;
			_rootNode.Items = _numItems;

			for (var i = 0; i < _numItems; ++i) {
				_rootNode.Bounds.Extend(bounds[i]);
				OrgIdx[i] = i;
			}

			_rootNode.CreateNextLevel(0, 0, this);
		}

		public void GetAabbOverlaps(in Entity entity, in BallData ball, ref DynamicBuffer<OverlappingDynamicBufferElement> overlappingEntities)
		{
			_rootNode.GetAabbOverlaps(ref this, in entity, in ball, ref overlappingEntities);
		}

		public Aabb GetItemAt(int i)
		{
			return _bounds[OrgIdx[i]];
		}

		public KdNode GetNodeAt(int i)
		{
			return _nodes[i];
		}

		public bool HasNodesAvailable()
		{
			// space for two more nodes?
			return NumNodes + 1 < _nodes.Length;
		}

		public void AddNodes(KdNode nodeA, KdNode nodeB, out int nodeIndexA, out int nodeIndexB)
		{
			NumNodes += 2;
			nodeIndexA = NumNodes - 2;
			nodeIndexB = NumNodes - 1;
			_nodes[nodeIndexA] = nodeA;
			_nodes[nodeIndexB] = nodeB;
		}

		public void Dispose()
		{
			_bounds.Dispose();
			OrgIdx.Dispose();
			Indices.Dispose();
		}
	}
}
