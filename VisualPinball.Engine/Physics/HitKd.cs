using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitKd
	{
		public int[] OrgIdx;                                                   // m_org_idx
		public int NumNodes;                                                   // m_num_nodes
		public int[] Indices;                                                  // tmp

		private readonly HitKdNode _rootNode;                                  // m_rootNode
		private List<HitObject> _orgHitObjects;                                // m_org_vho
		private HitKdNode[] _nodes;                                            // m_nodes

		private int _numItems;                                                 // m_num_items
		private int _maxItems;                                                 // m_max_items

		public HitKd()
		{
			_rootNode = new HitKdNode();
		}

		public void Init(List<HitObject> vho)
		{
			_orgHitObjects = vho;
			_numItems = vho.Count;

			if (_numItems > _maxItems) {
				OrgIdx = new int[_numItems];
				Indices = new int[_numItems];
				_nodes = new HitKdNode[(_numItems * 2 + 1) & ~1u];
             			}

			_maxItems = _numItems;
			NumNodes = 0;
			_rootNode.Reset();
		}

		public void FillFromVector(List<HitObject> vho)
		{
			Init(vho);

			_rootNode.RectBounds.Clear();
			_rootNode.Start = 0;
			_rootNode.Items = _numItems;

			for (var i = 0; i < _numItems; ++i) {
				var pho = vho[i];
				pho.CalcHitBBox(); //!! omit, as already calced?!
				_rootNode.RectBounds.Extend(pho.HitBBox);
				OrgIdx[i] = i;
			}

			_rootNode.CreateNextLevel(0, 0, this);
		}

		public HitObject GetItemAt(int i)
		{
			return _orgHitObjects[OrgIdx[i]];
		}

		public HitKdNode[] AllocTwoNodes()
		{
			if (NumNodes + 1 >= _nodes.Length) {
				// space for two more nodes?
				return null;
			}
			NumNodes += 2;
			return _nodes.Take(NumNodes - 2).ToArray();
		}
	}
}
