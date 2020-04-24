using System.Collections.Generic;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct KdRoot
	{
		public int[] OrgIdx;                                                   // m_org_idx
		public int NumNodes;                                                   // m_num_nodes
		public int[] Indices;                                                  // tmp

		private KdNode _rootNode;                                              // m_rootNode
		private Aabb[] _orgHitObjects;                                         // m_org_vho
		private KdNode[] _nodes;                                               // m_nodes

		private int _numItems;                                                 // m_num_items
		private int _maxItems;                                                 // m_max_items

		public KdRoot(Aabb[] bounds)
		{
			_orgHitObjects = bounds;
			_numItems = bounds.Length;
			_maxItems = _numItems;

			OrgIdx = new int[_numItems];
			Indices = new int[_numItems];
			_nodes = new KdNode[_numItems * 2 + 1];

			NumNodes = 0;
			_rootNode = new KdNode();
			_rootNode.Reset();

			FillFromVector(bounds);
		}

		private void FillFromVector(IReadOnlyList<Aabb> bounds)
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

		public ref Aabb GetItemAt(int i)
		{
			return ref _orgHitObjects[OrgIdx[i]];
		}

		public KdNode[] AllocTwoNodes()
		{
			if (NumNodes + 1 >= _nodes.Length) {
				// space for two more nodes?
				return null;
			}
			NumNodes += 2;
			return new[] { _nodes[NumNodes - 2], _nodes[NumNodes - 1]};
		}
	}
}
