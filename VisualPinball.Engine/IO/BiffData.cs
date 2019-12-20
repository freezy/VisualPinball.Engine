using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.IO
{
	/// <summary>
	/// The base class of all data classes dealing with BIFF records.<p/>
	///
	/// This statically indexes all fields and properties tagged with the Biff
	/// Attribute.
	/// </summary>
	public class BiffData
	{
		/// <summary>
		/// Indexes the BiffAttributes of the data class.<p/>
		///
		/// This must be run statically for every data class. I haven't found
		/// a way to do this automatically through inheritance, so it has to be
		/// done manually.
		/// </summary>
		/// <param name="type">Type of the data class</param>
		/// <param name="attributes">A dictionary to use for indexing</param>
		protected static void Init(Type type, Dictionary<string, BiffAttribute> attributes)
		{
			// get all fields and properties via reflection
			var members = type.GetMembers().Where(member => member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property);
			foreach (var member in members) {

				// for each field, see if there's one or more BiffAttributes
				var attrs = Attribute
					.GetCustomAttributes(member, typeof(BiffAttribute))
					.Select(a => a as BiffAttribute)
					.Where(a => a != null);

				// index for each attribute into a given dictionary
				foreach (var attr in attrs) {
					switch (member) {
						case FieldInfo field:
							attr.Field = field;
							break;
						case PropertyInfo property:
							attr.Property = property;
							break;
					}
					attributes[attr.Name] = attr;
				}
			}
		}

		/// <summary>
		/// Reads the binary data from the VPX file and applies them to the
		/// data class.
		/// </summary>
		/// <param name="obj">Object instance that is being read</param>
		/// <param name="reader">Binary data from the VPX file</param>
		/// <param name="attributes">The indexed Attributes of that data class</param>
		/// <typeparam name="T">Type of the data class</typeparam>
		/// <returns></returns>
		protected static T Load<T>(T obj, BinaryReader reader, Dictionary<string, BiffAttribute> attributes) where T : ItemData
		{
			// initially read length and BIFF record name
			var len = reader.ReadInt32();
			var tag = ReadTag(reader);

			while (tag != "ENDB") {

				if (attributes.ContainsKey(tag)) {
					var attr = attributes[tag];
					if (attr.IsStreaming) {
						len = reader.ReadInt32();
						attr.Parse(obj, reader, len);
					} else {
						attr.Parse(obj, reader, len - 4);
					}
				} else {
					Console.Error.WriteLine("[ItemData.Load] Unknown tag {0}", tag);
					reader.BaseStream.Seek(len - 4, SeekOrigin.Current);
				}

				// read next length and tag name for next record
				len = reader.ReadInt32();
				tag = ReadTag(reader);
			}
			return obj;
		}

		/// <summary>
		/// Returns the ASCII string of the next four (or less) chars on the
		/// current position of the stream.<p/>
		///
		/// Sometimes BIFF names are less than four chars, so we stop at 0x0.
		/// </summary>
		/// <param name="reader">Binary data from the VPX file, with pointer at the start of the BIFF tag</param>
		/// <returns>Name of the BIFF record</returns>
		private static string ReadTag(BinaryReader reader)
		{
			return Encoding.UTF8.GetString(reader.ReadBytes(4).Where(b => b != 0x0).ToArray());
		}
	}
}
