using System;
using System.Linq;
using System.Reflection;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using Xunit;

namespace VisualPinball.Engine.Test.IO
{
	public class BiffAttributeTest
	{
		[Fact]
		public void ShouldNotUseCountAndIndex()
		{
			GetAttributes(typeof(BiffAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (attr.Count > -1 && attr.Index > -1) {
					throw new Exception($"Must use either Count or Index but not both at {biffDataType.FullName}.{member.Name} ({attr.Name}).");
				}
			});
		}

		[Fact]
		public void ShouldBeAppliedToStrings()
		{
			GetAttributes(typeof(BiffStringAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(string) && memberType != typeof(string[])) {
					throw new Exception($"BiffString of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either string or string[], but is {memberType.Name}.");
				}
			});
		}

		[Fact]
		public void ShouldBeAppliedToIntegers()
		{
			GetAttributes(typeof(BiffIntAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(int) && memberType != typeof(int[])) {
					throw new Exception($"BiffInt of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either int or int[], but is {memberType.Name}.");
				}
			});
		}

		[Fact]
		public void ShouldBeAppliedToFloats()
		{
			GetAttributes(typeof(BiffFloatAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(float) && memberType != typeof(float[])) {
					throw new Exception($"BiffFloat of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either float or float[], but is {memberType.Name}.");
				}
			});
		}

		[Fact]
		public void ShouldBeAppliedToBooleans()
		{
			GetAttributes(typeof(BiffBoolAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(bool) && memberType != typeof(bool[])) {
					throw new Exception($"BiffBool of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either bool or bool[], but is {memberType.Name}.");
				}
			});
		}

		[Fact]
		public void ShouldBeAppliedToColors()
		{
			GetAttributes(typeof(BiffColorAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(Color) && memberType != typeof(Color[])) {
					throw new Exception($"BiffColor of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either Color or Color[], but is {memberType.Name}.");
				}
			});
		}

		[Fact]
		public void ShouldBeAppliedToVertices()
		{
			GetAttributes(typeof(BiffVertexAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(Vertex2D) && memberType != typeof(Vertex3D)) {
					throw new Exception($"BiffColor of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either Vertex2D or Vertex3D, but is {memberType.Name}.");
				}
			});
		}

		private static void GetAttributes(Type attributeType, Action<Type, MemberInfo, Type, BiffAttribute> assert)
		{
			var biffDataTypes = AppDomain.CurrentDomain
				.GetAssemblies()
				.First(a => a.GetName().Name == "VisualPinball.Engine")
				.GetTypes()
				.Where(t => t.IsSubclassOf(typeof(BiffData)))
				.ToArray();

			foreach (var biffDataType in biffDataTypes) {
				var members = biffDataType.GetMembers()
					.Where(member => member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property);

				foreach (var member in members) {
					var attrs = Attribute
						.GetCustomAttributes(member, attributeType)
						.Select(a => a as BiffAttribute)
						.Where(a => a != null);

					foreach (var attr in attrs) {
						Type memberType = null;
						switch (member) {
							case FieldInfo field:
								memberType = field.FieldType;
								break;
							case PropertyInfo property:
								memberType = property.PropertyType;
								break;
						}

						if (memberType == null) {
							throw new Exception("Member type is null, that shouldn't happen because we filter by fields and properties.");
						}

						assert(memberType, member, biffDataType, attr);
					}
				}
			}
		}
	}
}
