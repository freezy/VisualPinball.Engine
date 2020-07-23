using System;
using System.Collections.Generic;
using UnityEngine;


namespace VisualPinball.Unity.Editor.Utils.TreeView
{
	/// <summary>
	/// TreeElement is the base class for creating TreeModel which will be used by TreeViewWithTreeModel, you'll have to inherit from it.
	/// It provides base properties to be inserted & identified in a TreeView (Id, Name, Depth)
	/// It hosts also its hierarchical information.
	/// </summary>
	public abstract class TreeElement
	{
		public int Id { get; set; }
		public virtual string Name { get; }
		public int Depth { get; set; }
		public TreeElement Parent { get; set; }
		public List<TreeElement> Children { get; set; }
		public bool HasChildren => Children != null && Children.Count > 0;

		protected TreeElement ()
		{
		}

		protected TreeElement (string name, int depth, int id)
		{
			Name = name;
			Id = id;
			Depth = depth;
		}
	}

}


