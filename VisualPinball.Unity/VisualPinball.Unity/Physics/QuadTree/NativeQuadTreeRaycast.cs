using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public unsafe partial struct NativeQuadTree<T> where T : unmanaged
	{
		public struct Ray {
			public float2 from;
			public float2 to;
		}

		struct QuadTreeRaycast
		{
			NativeQuadTree<T> tree;

			UnsafeList* fastResults;
			int count;

			Ray ray;

			private int visited;

			public void Query(NativeQuadTree<T> tree, Ray ray, NativeList<QuadElement<T>> results)
			{
				this.tree = tree;
				this.ray = ray;
				count = 0;

				// Get pointer to inner list data for faster writing
				fastResults = (UnsafeList*) NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref results);

				// Query rest of tree
				RecursiveRangeQuery(tree.bounds, 1, 1);

				fastResults->Length = count;

			//	Debug.Log("visited: " + visited + " of " + tree.elementsCount);
			}

			public void RecursiveRangeQuery(Aabb2D parentBounds, int prevOffset, int depth)
			{
				if(count + 4 * tree.maxLeafElements > fastResults->Capacity)
				{
					fastResults->Resize<QuadElement<T>>(math.max(fastResults->Capacity * 2, count + 4 * tree.maxLeafElements));
				}

				var depthSize = LookupTables.DepthSizeLookup[tree.maxDepth - depth+1];
				for (int l = 0; l < 4; l++)
				{
					var childBounds = QuadTreeRangeQuery.GetChildBounds(parentBounds, l);

					if (!DoesIntersect(ray, childBounds)) {
						continue;
					}

					var at = prevOffset + l * depthSize;

					var elementCount = UnsafeUtility.ReadArrayElement<int>(tree.lookup->Ptr, at);

					if(elementCount > tree.maxLeafElements && depth < tree.maxDepth)
					{
						RecursiveRangeQuery(childBounds, at+1, depth+1);
					}
					else if(elementCount != 0)
					{
						var node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodes->Ptr, at);

						for (int k = 0; k < node.count; k++)
						{
							var element = UnsafeUtility.ReadArrayElement<QuadElement<T>>(tree.elements->Ptr, node.firstChildIndex + k);
							if(DoesIntersect(ray, element.bounds))
							{
								UnsafeUtility.WriteArrayElement(fastResults->Ptr, count++, element);
							}

							visited++;
						}
					}
				}
			}

			private static bool DoesIntersect(Ray ray, Aabb2D bounds) {
				return AabbContainsSegment(ray.from.x, ray.from.y, ray.to.x, ray.to.y, bounds.Min.x, bounds.Min.y,
					bounds.Max.x, bounds.Max.y);
			}

			private static bool AabbContainsSegment (float x1, float y1, float x2, float y2, float minX, float minY, float maxX, float maxY) {
				// Completely outside.
				if ((x1 <= minX && x2 <= minX) || (y1 <= minY && y2 <= minY) || (x1 >= maxX && x2 >= maxX) || (y1 >= maxY && y2 >= maxY))
					return false;

				float m = (y2 - y1) / (x2 - x1);

				float y = m * (minX - x1) + y1;
				if (y > minY && y < maxY) return true;

				y = m * (maxX - x1) + y1;
				if (y > minY && y < maxY) return true;

				float x = (minY - y1) / m + x1;
				if (x > minX && x < maxX) return true;

				x = (maxY - y1) / m + x1;
				if (x > minX && x < maxX) return true;

				return false;
			}
		}

	}
}
