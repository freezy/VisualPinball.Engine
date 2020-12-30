// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using UnityEngine;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Text;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// VPE editor base class with enhanced functionality.
	/// Example: You can access members of the MonoBehaviour via e. g. <code>FindProperty( x => x.cameraPresets);</code> instead of <code>serializedObject.FindProperty("cameraPresets");</code>
	/// </summary>
	/// <typeparam name="T">The editor target</typeparam>
	public class BaseEditor<T> : UnityEditor.Editor where T : MonoBehaviour
	{
		protected T Target => (T)target;

		/// <summary>
		/// Get a property using reflection and in that way avoiding having to hardcode members as strings.
		/// </summary>
		/// <typeparam name="TValue"></typeparam>
		/// <param name="expression"></param>
		/// <returns></returns>
		protected UnityEditor.SerializedProperty FindProperty<TValue>(Expression<Func<T, TValue>> expression)
		{
			return serializedObject.FindProperty(GetFieldPath(expression));
		}

		/// <summary>
		/// Get the field path as string from an expression.
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <typeparam name="TValue"></typeparam>
		/// <param name="expression"></param>
		/// <returns></returns>
		private static string GetFieldPath<TType, TValue>(Expression<Func<TType, TValue>> expression)
		{
			MemberExpression memberExpression;
			switch (expression.Body.NodeType)
			{
				case ExpressionType.MemberAccess:
					memberExpression = expression.Body as MemberExpression;
					break;
				default:
					throw new InvalidOperationException();
			}

			var members = new List<string>();
			while (memberExpression != null)
			{
				members.Add(memberExpression.Member.Name);
				memberExpression = memberExpression.Expression as MemberExpression;
			}

			var stringBuilder = new StringBuilder();
			for (var i = members.Count - 1; i >= 0; i--)
			{
				stringBuilder.Append(members[i]);

				if (i > 0)
				{
					stringBuilder.Append('.');
				}
			}

			return stringBuilder.ToString();
		}
	}
}
