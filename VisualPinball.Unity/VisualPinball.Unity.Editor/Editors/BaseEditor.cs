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
        protected T Target
        {
            get { return (T)target; }
        }

		/// <summary>
		/// Get a property using reflection and in that way avoiding having to hardcode members as strings.
		/// </summary>
		/// <typeparam name="TValue"></typeparam>
		/// <param name="expression"></param>
		/// <returns></returns>
        public UnityEditor.SerializedProperty FindProperty<TValue>(Expression<Func<T, TValue>> expression)
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
		public static string GetFieldPath<TType, TValue>(Expression<Func<TType, TValue>> expression)
		{
			MemberExpression memberExpression = expression.Body.NodeType switch
			{
				ExpressionType.MemberAccess => expression.Body as MemberExpression,
				_ => throw new InvalidOperationException(),
			};

			var members = new List<string>();
			while (memberExpression != null)
			{
				members.Add(memberExpression.Member.Name);
				memberExpression = memberExpression.Expression as MemberExpression;
			}

			StringBuilder stringBuilder = new StringBuilder();
			for (int i = members.Count - 1; i >= 0; i--)
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
