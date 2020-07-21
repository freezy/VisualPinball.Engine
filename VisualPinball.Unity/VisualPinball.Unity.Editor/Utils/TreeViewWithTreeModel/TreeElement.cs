using System;
using System.Collections.Generic;
using UnityEngine;


namespace VisualPinball.Unity.Editor.Utils.TreeViewWithTreeModel
{

	/// <summary>
	/// TreeElement is the base class for creating TreeModel which will be used by TreeViewWithTreeModel, you'll have to inherit from it.
	/// It provides base properties to be inserted & identified in a TreeView (Id, Name, Depth)
	/// It hosts also its hierarchical information.
	/// Some properties are serializable to save tree structure as Assets.
	/// </summary>
	[Serializable]
	public class TreeElement
	{
		[SerializeField]
		public int Id { get; set; }
		[SerializeField]
		public virtual string Name { get; }

		[SerializeField]
		public int Depth { get; set; }

		[NonSerialized]
		private TreeElement _parent;
		public TreeElement Parent { get { return _parent; } set { _parent = value; } }

		[NonSerialized]
		private List<TreeElement> _children;
		public List<TreeElement> Children { get { return _children; } set { _children = value; } }
		public bool HasChildren { get { return Children != null && Children.Count > 0; } }

		public TreeElement ()
		{
		}

		public TreeElement (string name, int depth, int id)
		{
			Name = name;
			Id = id;
			Depth = depth;
		}
	}

}


