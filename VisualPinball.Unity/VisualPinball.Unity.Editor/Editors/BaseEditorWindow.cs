using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Base EditorWindow class
	/// Providing basic services for all VPE Editors
	/// </summary>
	/// <remarks>
	/// <see cref="SearchableEditorWindow"/> : provide a search filter GUI which can be synchronized between other <see cref="SearchableEditorWindow"/> (<see cref="SceneHierarchyWindow"/> & <see cref="SceneView"/> are implementing <see cref="SearchableEditorWindow"/>)
	/// </remarks>
	public abstract class BaseEditorWindow : SearchableEditorWindow
	{
		#region SearchableEditorWindow
		/// <summary>
		/// The search group this Editor belongs to
		/// </summary>
		/// <remarks>
		/// The only active searchgroup for now is the 0 (<see cref="SceneHierarchyWindow"/>, <see cref="SceneView"/>)
		/// </remarks>
		protected virtual int SearchGroup => -1;
		/// <summary>
		/// The hierarchy type of this editor
		/// </summary>
		/// <remarks>
		/// It has to be different from HierarchyType.Assets, <see cref="SearchableEditorWindow"/> with HierarchyType.Assets are excluded from synchronization in UnityEditor
		/// </remarks>
		protected virtual HierarchyType HierarchyType => HierarchyType.Assets;

		/// <summary>
		/// Reflection info retrieved from non exposed fields/methods in the <see cref="SearchableEditorWindow"/>
		/// </summary>
		private FieldInfo _hasFilterFocusInfo;
		private FieldInfo _searchFilterInfo;
		private MethodInfo _searchFieldGUIInfo;

		/// <summary>
		/// This delegate will be called when the synchronized search filter is focused and Up or Down arrow is pressed
		/// </summary>
		public Action SyncSearchFieldDownOrUpArrowPressed;

		/// <summary>
		/// Display the <see cref="SearchableEditorWindow"/> search field
		/// </summary>
		/// <param name="width">the width of the search field</param>
		/// <returns>The value of the search field</returns>
		/// <remarks>
		/// Invoking this method will synchronize other <see cref="SearchableEditorWindow"/> with the same search group & hierarchy type 
		/// </remarks>
		protected string SyncSearchFieldGUI(float width)
		{
			// When searchfield has focus call delegates on Down/UpArrow
			Event evt = Event.current;
			if (SyncSearchFieldDownOrUpArrowPressed != null && (bool)_hasFilterFocusInfo?.GetValue(this) && evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow)) {
				SyncSearchFieldDownOrUpArrowPressed.Invoke();
				evt.Use();
			}

			object[] parameters = new object[] { width };
			_searchFieldGUIInfo?.Invoke(this, parameters);
			string newFilter = _searchFilterInfo?.GetValue(this) as string;
			return newFilter;
		}

		#endregion

		public override void OnEnable()
		{
			#region SearchableEditorWindow setup
			//Retrieve m_SearchFilter field info for returning the new filter data
			_searchFilterInfo = typeof(SearchableEditorWindow).GetField("m_SearchFilter", BindingFlags.NonPublic | BindingFlags.Instance);
			//Retrieve SearchFilterGUI(float width) method info
			_searchFieldGUIInfo = typeof(SearchableEditorWindow).GetMethod("SearchFieldGUI", BindingFlags.NonPublic | BindingFlags.Instance, null, CallingConventions.Any, new System.Type[] { typeof(float) }, null);
			//Retrieve m_HasSearchFilterFocus field info 
			_hasFilterFocusInfo = typeof(SearchableEditorWindow).GetField("m_HasSearchFilterFocus", BindingFlags.NonPublic | BindingFlags.Instance);


			//Setup this Editor to its searchgroup & hierarchyType
			m_SearchGroup = SearchGroup;
			FieldInfo hierarchyTypeInfo = typeof(SearchableEditorWindow).GetField("m_HierarchyType", BindingFlags.NonPublic | BindingFlags.Instance);
			hierarchyTypeInfo?.SetValue(this, HierarchyType);
			#endregion

			base.OnEnable();
		}

	}
}
