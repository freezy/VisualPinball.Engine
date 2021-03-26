using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Editor drawing of the NativeQuadTree
	/// </summary>
	public unsafe partial struct NativeQuadTree<T> where T : unmanaged
	{
		public static void Draw(NativeQuadTree<T> tree, NativeList<QuadElement<T>> results, Color[][] texture)
		{
			var widthMult = texture.Length / tree.bounds.Extents.x * 2 / 2 / 2;
			var heightMult = texture[0].Length / tree.bounds.Extents.y * 2 / 2 / 2;

			var widthAdd = tree.bounds.Center.x + tree.bounds.Extents.x;
			var heightAdd = tree.bounds.Center.y + tree.bounds.Extents.y;

			for (int i = 0; i < tree.nodes->Capacity; i++)
			{
				var node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodes->Ptr, i);

				if(node.count > 0)
				{
					for (int k = 0; k < node.count; k++)
					{
						var element =
							UnsafeUtility.ReadArrayElement<QuadElement<T>>(tree.elements->Ptr, node.firstChildIndex + k);

						texture[(int) ((element.bounds.Center.x + widthAdd) * widthMult)]
							[(int) ((element.bounds.Center.y + heightAdd) * heightMult)] = Color.red;
					}
				}
			}

			foreach (var element in results)
			{
				texture[(int) ((element.bounds.Center.x + widthAdd) * widthMult)]
					[(int) ((element.bounds.Center.y + heightAdd) * heightMult)] = Color.green;
			}
		}

		public static void DrawBounds(Color[][] texture, Aabb2D bounds, NativeQuadTree<T> tree)
		{
			var widthMult = texture.Length / tree.bounds.Extents.x * 2 / 2 / 2;
			var heightMult = texture[0].Length / tree.bounds.Extents.y * 2 / 2 / 2;

			var widthAdd = tree.bounds.Center.x + tree.bounds.Extents.x;
			var heightAdd = tree.bounds.Center.y + tree.bounds.Extents.y;

			var top = new float2(bounds.Center.x, bounds.Center.y - bounds.Extents.y);
			var left = new float2(bounds.Center.x - bounds.Extents.x, bounds.Center.y);

			for (int leftToRight = 0; leftToRight < bounds.Extents.x * 2; leftToRight++)
			{
				var poxX = left.x + leftToRight;
				texture[(int) ((poxX + widthAdd) * widthMult)][(int) ((bounds.Center.y + heightAdd + bounds.Extents.y) * heightMult)] = Color.blue;
				texture[(int) ((poxX + widthAdd) * widthMult)][(int) ((bounds.Center.y + heightAdd - bounds.Extents.y) * heightMult)] = Color.blue;
			}

			for (int topToBottom = 0; topToBottom < bounds.Extents.y * 2; topToBottom++)
			{
				var posY = top.y + topToBottom;
				texture[(int) ((bounds.Center.x + widthAdd + bounds.Extents.x) * widthMult)][(int) ((posY + heightAdd) * heightMult)] = Color.blue;
				texture[(int) ((bounds.Center.x + widthAdd - bounds.Extents.x) * widthMult)][(int) ((posY + heightAdd) * heightMult)] = Color.blue;
			}
		}
	}
}
