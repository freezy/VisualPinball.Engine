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
using System.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.IO
{
	public class BiffVertexAttribute : BiffAttribute
	{
		public bool IsPadded = false;
		public bool WriteAsVertex2D = false;

		public BiffVertexAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadVertex2D);
			ParseValue(obj, reader, len, ReadVertex3D);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			if (Type == typeof(Vertex2D)) {
				WriteValue<TItem, Vertex2D>(
					obj,
					writer,
					(w, v) =>  WriteVertex2D(w, v, hashWriter),
					hashWriter);

			} else if (Type == typeof(Vertex3D)) {
				WriteValue<TItem, Vertex3D>(
					obj,
					writer,
					(w, v) => WriteVertex3D(w, v, hashWriter),
					hashWriter);

			} else {
				throw new InvalidOperationException("Unknown type for [BiffVertex] on field \"" + Name + "\".");
			}
		}

		private static Vertex2D ReadVertex2D(BinaryReader reader, int len)
		{
			return new Vertex2D(reader, len);
		}

		private static Vertex3D ReadVertex3D(BinaryReader reader, int len)
		{
			return new Vertex3D(reader, len);
		}

		private void WriteVertex2D(BinaryWriter writer, Vertex2D value, HashWriter hashWriter)
		{
			writer.Write(value.X);
			writer.Write(value.Y);
		}

		private void WriteVertex3D(BinaryWriter writer, Vertex3D value, HashWriter hashWriter)
		{
			writer.Write(value.X);
			writer.Write(value.Y);
			if (!WriteAsVertex2D) {
				writer.Write(value.Z);
				if (IsPadded) {
					writer.Write(0f);
				}
			}
		}
	}
}
