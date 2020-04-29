using System;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct KdRoot : IDisposable
	{
		public NativeArray<int> OrgIdx;                                                   // m_org_idx
		public int NumNodes;                                                   // m_num_nodes
		public NativeArray<int> Indices;                                                  // tmp

		private KdNode _rootNode;                                              // m_rootNode
		private NativeArray<Aabb> _orgHitObjects;                                         // m_org_vho
		private NativeArray<KdNode> _nodes;                                               // m_nodes

		private int _numItems;                                                 // m_num_items
		private int _maxItems;                                                 // m_max_items

		public KdRoot(NativeArray<Aabb> bounds)
		{
			_orgHitObjects = bounds;
			_numItems = bounds.Length;
			_maxItems = _numItems;

			OrgIdx = new NativeArray<int>(_numItems, Allocator.Temp);
			Indices = new NativeArray<int>(_numItems, Allocator.Temp);
			_nodes = new NativeArray<KdNode>(_numItems * 2 + 1, Allocator.Temp);

			NumNodes = 0;
			_rootNode = new KdNode();
			_rootNode.Reset();

			FillFromVector(bounds);
		}

		private void FillFromVector(NativeArray<Aabb> bounds)
		{
			_rootNode.RectBounds.Clear();
			_rootNode.Start = 0;
			_rootNode.Items = _numItems;

			for (var i = 0; i < _numItems; ++i) {
				_rootNode.RectBounds.Extend(bounds[i]);
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
			return _orgHitObjects[OrgIdx[i]];
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

		public void AddNodes(KdNode nodeA, KdNode nodeB)
		{
			NumNodes += 2;
			_nodes[NumNodes - 2] = nodeA;
			_nodes[NumNodes - 1] = nodeB;
		}

		public void Dispose()
		{
			OrgIdx.Dispose();
			Indices.Dispose();
		}
	}
}
