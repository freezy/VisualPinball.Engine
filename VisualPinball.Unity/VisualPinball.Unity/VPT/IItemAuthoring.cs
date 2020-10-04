using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The non-typed version of ItemAuthoring.
	/// </summary>
	public interface IItemAuthoring
	{
		string Name { get; }

		IItem IItem { get; }

		ItemData ItemData { get; }
	}
}
