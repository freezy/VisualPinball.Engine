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
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Test.IO
{
	public class BiffAttributeTest
	{
		[Test]
		public void ShouldNotUseCountAndIndex()
		{
			GetAttributes<BiffAttribute>(typeof(BiffAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (attr.Count > -1 && attr.Index > -1) {
					throw new Exception($"Must use either Count or Index but not both at {biffDataType.FullName}.{member.Name} ({attr.Name}).");
				}
			});
		}

		[Test]
		public void ShouldBeAppliedToStrings()
		{
			GetAttributes<BiffStringAttribute>(typeof(BiffStringAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(string) && memberType != typeof(string[])) {
					throw new Exception($"BiffString of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either string or string[], but is {memberType.Name}.");
				}
			});
		}

		[Test]
		public void ShouldBeAppliedToIntegers()
		{
			GetAttributes<BiffIntAttribute>(typeof(BiffIntAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(int) && memberType != typeof(int[])) {
					throw new Exception($"BiffInt of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either int or int[], but is {memberType.Name}.");
				}
			});
		}

		[Test]
		public void ShouldBeAppliedToFloats()
		{
			GetAttributes<BiffFloatAttribute>(typeof(BiffFloatAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (!attr.AsInt) {
					if (memberType != typeof(float) && memberType != typeof(float[])) {
						throw new Exception($"BiffFloat of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either float or float[], but is {memberType.Name}.");
					}
				} else {
					if (memberType != typeof(int) && memberType != typeof(int[])) {
						throw new Exception($"BiffFloat of {biffDataType.FullName}.{member.Name} ({attr.Name}) is marked to be int or int[], but is {memberType.Name}.");
					}
				}
			});
		}

		[Test]
		public void ShouldBeAppliedToBooleans()
		{
			GetAttributes<BiffBoolAttribute>(typeof(BiffBoolAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(bool) && memberType != typeof(bool[])) {
					throw new Exception($"BiffBool of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either bool or bool[], but is {memberType.Name}.");
				}
			});
		}

		[Test]
		public void ShouldBeAppliedToColors()
		{
			GetAttributes<BiffColorAttribute>(typeof(BiffColorAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(Color) && memberType != typeof(Color[])) {
					throw new Exception($"BiffColor of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either Color or Color[], but is {memberType.Name}.");
				}
			});
		}

		[Test]
		public void ShouldBeAppliedToVertices()
		{
			GetAttributes<BiffVertexAttribute>(typeof(BiffVertexAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(Vertex2D) && memberType != typeof(Vertex3D)) {
					throw new Exception($"BiffVertex of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be either Vertex2D or Vertex3D, but is {memberType.Name}.");
				}
			});
		}

		[Test]
		public void ShouldBeAppliedToTextureBinary()
		{
			GetAttributes<BiffBinaryAttribute>(typeof(BiffBinaryAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(BinaryData)) {
					throw new Exception($"BiffBinary of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be of type BinaryData, but is {memberType.Name}.");
				}
			});
		}

		[Test]
		public void ShouldBeAppliedToTextureBits()
		{
			GetAttributes<BiffBitsAttribute>(typeof(BiffBitsAttribute), (memberType, member, biffDataType, attr) =>
			{
				if (memberType != typeof(Bitmap)) {
					throw new Exception($"BiffBits of {biffDataType.FullName}.{member.Name} ({attr.Name}) must be of type Bitmap, but is {memberType.Name}.");
				}
			});
		}

		private static void GetAttributes<T>(Type attributeType, Action<Type, MemberInfo, Type, T> assert) where T: BiffAttribute
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

						assert(memberType, member, biffDataType, attr as T);
					}
				}
			}
		}
	}
}
