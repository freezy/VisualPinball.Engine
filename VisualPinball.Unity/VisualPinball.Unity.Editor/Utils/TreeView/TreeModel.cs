using System;
using System.Collections.Generic;
using System.Linq;


namespace VisualPinball.Unity.Editor.Utils.TreeView
{
	/// <summary>
	/// The TreeModel is a utility class working on a list of serializable TreeElements where the order and the depth
	/// of each TreeElement define the tree structure.
	///
	/// The tree representation (parent and children references) are then build internally using
	/// TreeElementUtility.ListToTree (using depth values of the elements).
	///
	/// Some tree manipulation helpers are provided and a modelChanged event is fired using those modification methods.
	/// </summary>
	/// <typeparam name="T">TreeElement derived class in the TreeModel</typeparam>
	/// <remarks>
	/// Note that the TreeModel itself is not serializable (in Unity we are currently limited to serializing lists/arrays)
	/// but the input list is.
	///
	/// The first element of the input list is required to have depth == -1 (the hiddenroot) and the rest to have
	/// depth >= 0 (otherwise an exception will be thrown)
	/// </remarks>
	public class TreeModel<T> where T : TreeElement
	{
		private  IList<T> _data;
		private int _maxID;

		public T Root { get; private set; }
		public event Action modelChanged;

		public TreeModel(IList<T> data)
		{
			SetData(data);
		}

		public T Find(int id)
		{
			return _data.FirstOrDefault(element => element.Id == id);
		}

		public void SetData(IList<T> data)
		{
			Init(data);
		}

		void Init (IList<T> data)
		{
			if (data == null)
				throw new ArgumentNullException("data", "Input data is null. Ensure input is a non-null list.");

			_data = data;
			if (_data.Count > 0)
				Root = TreeElementUtility.ListToTree(data);

			_maxID = _data.Max(e => e.Id);
		}

		public IList<int> GetAncestors (int id)
		{
			var parents = new List<int>();
			TreeElement T = Find(id);
			if (T != null)
			{
				while (T.Parent != null)
				{
					parents.Add(T.Parent.Id);
					T = T.Parent;
				}
			}
			return parents;
		}

		public IList<int> GetDescendantsThatHaveChildren (int id)
		{
			T searchFromThis = Find(id);
			if (searchFromThis != null)
			{
				return GetParentsBelowStackBased(searchFromThis);
			}
			return new List<int>();
		}

		IList<int> GetParentsBelowStackBased(TreeElement searchFromThis)
		{
			Stack<TreeElement> stack = new Stack<TreeElement>();
			stack.Push(searchFromThis);

			var parentsBelow = new List<int>();
			while (stack.Count > 0)
			{
				TreeElement current = stack.Pop();
				if (current.HasChildren)
				{
					parentsBelow.Add(current.Id);
					foreach (var T in current.Children)
					{
						stack.Push(T);
					}
				}
			}

			return parentsBelow;
		}
	}
}
