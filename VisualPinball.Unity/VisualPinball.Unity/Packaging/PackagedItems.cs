// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// <c>table/items.json</c>: the GameObject state and the component data of every node, in one file.
	/// </summary>
	///
	/// <remarks>
	/// <code>
	/// {"nodes":[
	/// {"id":"&lt;nodeId&gt;","item":{…},"components":[{"type":"Flipper","data":{…},"refs":{…}},{"type":"LightGroup","refs":{…}}]},
	/// {"id":"&lt;nodeId&gt;","item":{…}}
	/// ]}
	/// </code>
	/// The order of <c>nodes</c> is the order they are restored in, and the order of a node's <c>components</c> the order
	/// they are created in: the n-th entry of a type is the n-th component of exactly that type on the node. An entry's
	/// <c>data</c> and <c>refs</c> belong to that one component. <c>data</c> is applied when the component is created
	/// and is an empty object when left out; <c>refs</c> is applied in a second pass, once every component exists.
	/// <para>
	/// The values of <c>item</c>, <c>data</c> and <c>refs</c> belong to the components and are handed to them as they
	/// are written, so this reader cuts them out of the file instead of parsing them: reading a table's items costs
	/// one pass over the bytes, and each component still parses only its own payload.
	/// </para>
	/// </remarks>
	public static class PackagedItems
	{
		/// <summary>How deep a written payload may nest. Newtonsoft, which parses the payloads, stops at 64 too.</summary>
		private const int MaxPayloadDepth = 64;

		/// <summary>How deep the reader follows a value. More than a writer produces, so that it reads all of them.</summary>
		private const int MaxDepth = 128;

		private static readonly UTF8Encoding Utf8 = new(false, true);

		/// <summary>A node of the scene and what the package stores for it.</summary>
		public sealed class Node
		{
			/// <summary>The node's id, as in <c>extras.vpeId</c> of the scene.</summary>
			public string Id;

			/// <summary>The GameObject's state, see <see cref="ItemPackable"/>. Null when the package stores none.</summary>
			public byte[] Item;

			/// <summary>The node's components, in the order they are created.</summary>
			public readonly List<Entry> Components = new();
		}

		/// <summary>One component of a node.</summary>
		public sealed class Entry
		{
			/// <summary>The component's registered pack name, see <see cref="PackAsAttribute"/>.</summary>
			public string Type;

			/// <summary>What <see cref="IPackable.Pack"/> returned. Null stands for an empty object.</summary>
			public byte[] Data;

			/// <summary>What <see cref="IPackable.PackReferences"/> returned, or null when the component has none.</summary>
			public byte[] Refs;
		}

		/// <summary>
		/// Checks that a component's payload is one complete value of strict JSON, so that it can't break the file it
		/// is written into for any reader.
		/// </summary>
		///
		/// <remarks>
		/// Strict, because the parsers at hand take more than JSON (comments, single quotes, bare names, trailing
		/// commas), and what one of them takes, the reader that cuts the file by its brackets or another engine's
		/// parser may read differently.
		/// </remarks>
		/// <exception cref="InvalidDataException">The payload is not strict JSON, or nests deeper than a reader accepts.</exception>
		public static void ValidatePayload(byte[] payload)
		{
			try {
				Utf8.GetString(payload);
			} catch (ArgumentException) {
				throw new InvalidDataException("Invalid payload: it is not UTF-8.");
			}
			var scanner = new Scanner(payload, "payload", false);
			scanner.ValidateValue(0);
			scanner.ExpectEnd();
		}

		/// <summary>Writes the nodes, one per line. Payloads go into the file as they are, see <see cref="ValidatePayload"/>.</summary>
		public static byte[] Write(IReadOnlyList<Node> nodes)
		{
			var ids = new HashSet<string>(StringComparer.Ordinal);
			using var stream = new MemoryStream();
			Append(stream, "{\"nodes\":[");
			for (var i = 0; i < nodes.Count; i++) {
				var node = nodes[i];
				if (string.IsNullOrEmpty(node.Id) || !ids.Add(node.Id)) {
					throw new InvalidOperationException($"Node id '{node.Id}' is missing or used twice.");
				}
				Append(stream, i == 0 ? "\n{\"id\":" : ",\n{\"id\":");
				Append(stream, JsonConvert.ToString(node.Id));
				AppendPayload(stream, ",\"item\":", node.Item);
				if (node.Components.Count > 0) {
					Append(stream, ",\"components\":[");
					for (var j = 0; j < node.Components.Count; j++) {
						var entry = node.Components[j];
						if (string.IsNullOrEmpty(entry.Type)) {
							throw new InvalidOperationException($"A component of node '{node.Id}' has no type name.");
						}
						Append(stream, j == 0 ? "{\"type\":" : ",{\"type\":");
						Append(stream, JsonConvert.ToString(entry.Type));
						AppendPayload(stream, ",\"data\":", entry.Data);
						AppendPayload(stream, ",\"refs\":", entry.Refs);
						stream.WriteByte((byte)'}');
					}
					stream.WriteByte((byte)']');
				}
				stream.WriteByte((byte)'}');
			}
			Append(stream, "\n]}");
			return stream.ToArray();
		}

		private static void Append(Stream stream, string text)
		{
			var bytes = Utf8.GetBytes(text);
			stream.Write(bytes, 0, bytes.Length);
		}

		private static void AppendPayload(Stream stream, string key, byte[] payload)
		{
			if (payload == null || payload.Length == 0) {
				return;
			}
			Append(stream, key);
			stream.Write(payload, 0, payload.Length);
		}

		/// <summary>Reads the nodes of a package, given its table folder. A package without the file has none.</summary>
		public static List<Node> Read(IPackageFolder tableFolder)
		{
			return tableFolder.TryGetFile(PackageApi.ItemsFile, out var file, PackageApi.Packer.FileExtension)
				? Read(file.GetData())
				: new List<Node>();
		}

		/// <summary>
		/// The component an entry stands for: the <paramref name="index"/>-th one of exactly this type on the object, or
		/// null when the object has fewer and the component is to be added.
		/// </summary>
		public static Component FindComponent(GameObject gameObject, Type type, int index)
		{
			foreach (var component in gameObject.GetComponents(type)) {
				// GetComponents also returns the types derived from it, which have entries of their own
				if (component && component.GetType() == type && index-- == 0) {
					return component;
				}
			}
			return null;
		}

		/// <summary>Reads the nodes of an items file.</summary>
		/// <exception cref="InvalidDataException">The file is not what <see cref="Write"/> produces: broken JSON, a node without an id or with one used before, a component without a type.</exception>
		public static List<Node> Read(byte[] json)
		{
			var nodes = new List<Node>();
			var scanner = new Scanner(json, PackageApi.ItemsFile + PackageApi.Packer.FileExtension, true);
			var sawNodes = false;
			scanner.ReadObject(key => {
				if (key != "nodes") {
					scanner.SkipValue();
					return;
				}
				if (sawNodes) {
					throw scanner.Error("'nodes' is there twice");
				}
				sawNodes = true;
				var ids = new HashSet<string>(StringComparer.Ordinal);
				scanner.ReadArray(() => {
					var node = ReadNode(scanner);
					if (!ids.Add(node.Id)) {
						throw scanner.Error($"node '{node.Id}' is there twice");
					}
					nodes.Add(node);
				});
			});
			scanner.ExpectEnd();
			return nodes;
		}

		private static Node ReadNode(Scanner scanner)
		{
			var node = new Node();
			var seen = 0;
			scanner.ReadObject(key => {
				switch (key) {
					case "id":
						Once(scanner, ref seen, 1, key);
						node.Id = scanner.ReadString();
						break;
					case "item":
						Once(scanner, ref seen, 2, key);
						node.Item = scanner.ReadPayload();
						break;
					case "components":
						Once(scanner, ref seen, 4, key);
						scanner.ReadArray(() => node.Components.Add(ReadEntry(scanner)));
						break;
					default:
						scanner.SkipValue();
						break;
				}
			});
			if (string.IsNullOrEmpty(node.Id)) {
				throw scanner.Error("a node has no id");
			}
			return node;
		}

		private static Entry ReadEntry(Scanner scanner)
		{
			var entry = new Entry();
			var seen = 0;
			scanner.ReadObject(key => {
				switch (key) {
					case "type":
						Once(scanner, ref seen, 1, key);
						entry.Type = scanner.ReadString();
						break;
					case "data":
						Once(scanner, ref seen, 2, key);
						entry.Data = scanner.ReadPayload();
						break;
					case "refs":
						Once(scanner, ref seen, 4, key);
						entry.Refs = scanner.ReadPayload();
						break;
					default:
						scanner.SkipValue();
						break;
				}
			});
			if (string.IsNullOrEmpty(entry.Type)) {
				throw scanner.Error("a component has no type");
			}
			return entry;
		}

		private static void Once(Scanner scanner, ref int seen, int flag, string key)
		{
			if ((seen & flag) != 0) {
				throw scanner.Error($"'{key}' is there twice");
			}
			seen |= flag;
		}

		/// <summary>
		/// Walks the structure of the file and finds where each value ends, without building anything for the values it
		/// only hands on.
		/// </summary>
		private sealed class Scanner
		{
			private readonly byte[] _json;
			private readonly string _name;
			private int _pos;

			/// <param name="json">The bytes to walk.</param>
			/// <param name="name">What they are, for the errors.</param>
			/// <param name="isFile">A file may start with a byte order mark. A value inside a file may not.</param>
			public Scanner(byte[] json, string name, bool isFile)
			{
				_json = json ?? throw new ArgumentNullException(nameof(json));
				_name = name;
				if (isFile && json.Length >= 3 && json[0] == 0xEF && json[1] == 0xBB && json[2] == 0xBF) {
					_pos = 3;
				}
			}

			public InvalidDataException Error(string what) => new($"Invalid {_name} near byte {_pos}: {what}.");

			public void ReadObject(Action<string> member)
			{
				Expect('{');
				if (TryConsume('}')) {
					return;
				}
				do {
					var key = ReadString();
					Expect(':');
					member(key);
				} while (TryConsume(','));
				Expect('}');
			}

			public void ReadArray(Action element)
			{
				Expect('[');
				if (TryConsume(']')) {
					return;
				}
				do {
					element();
				} while (TryConsume(','));
				Expect(']');
			}

			public void ExpectEnd()
			{
				SkipWhitespace();
				if (_pos != _json.Length) {
					throw Error("it continues after its value");
				}
			}

			public string ReadString()
			{
				SkipWhitespace();
				var start = _pos;
				var escaped = SkipString();
				if (!escaped) {
					return Decode(start + 1, _pos - start - 2);
				}
				try {
					using var reader = new JsonTextReader(new StringReader(Decode(start, _pos - start))) { DateParseHandling = DateParseHandling.None };
					return reader.Read() && reader.TokenType == JsonToken.String ? (string)reader.Value : throw Error("a string can't be read");
				} catch (JsonException) {
					throw Error("a string can't be read");
				}
			}

			/// <summary>The bytes of the next value, or null when it is <c>null</c>.</summary>
			public byte[] ReadPayload()
			{
				SkipWhitespace();
				var start = _pos;
				SkipValue();
				var length = _pos - start;
				if (length == 4 && _json[start] == 'n' && _json[start + 1] == 'u' && _json[start + 2] == 'l' && _json[start + 3] == 'l') {
					return null;
				}
				var payload = new byte[length];
				Buffer.BlockCopy(_json, start, payload, 0, length);
				return payload;
			}

			public void SkipValue()
			{
				SkipWhitespace();
				var c = Peek();
				if (c == '"') {
					SkipString();
					return;
				}
				if (c != '{' && c != '[') {
					var start = _pos;
					while (_pos < _json.Length && IsScalar(_json[_pos])) {
						_pos++;
					}
					if (_pos == start) {
						throw Error("a value is missing");
					}
					return;
				}

				// Only the brackets and the strings decide where a value ends. What is between them is the business of
				// whoever parses the payload.
				Span<byte> open = stackalloc byte[MaxDepth];
				var depth = 0;
				while (true) {
					c = Peek();
					switch (c) {
						case (byte)'"':
							SkipString();
							continue;
						case (byte)'{':
						case (byte)'[':
							if (depth == MaxDepth) {
								throw Error("a value nests too deep");
							}
							open[depth++] = c;
							break;
						case (byte)'}':
						case (byte)']':
							if (open[depth - 1] != (c == '}' ? '{' : '[')) {
								throw Error("a bracket closes what another one opened");
							}
							if (--depth == 0) {
								_pos++;
								return;
							}
							break;
					}
					_pos++;
				}
			}

			/// <summary>Moves past the next value if it is strict JSON, nested no deeper than a payload may be.</summary>
			public void ValidateValue(int depth)
			{
				SkipWhitespace();
				switch (Peek()) {
					case (byte)'"':
						ValidateString();
						return;
					case (byte)'{':
						EnterContainer(depth);
						if (TryConsume('}')) {
							return;
						}
						do {
							SkipWhitespace();
							ValidateString();
							Expect(':');
							ValidateValue(depth + 1);
						} while (TryConsume(','));
						Expect('}');
						return;
					case (byte)'[':
						EnterContainer(depth);
						if (TryConsume(']')) {
							return;
						}
						do {
							ValidateValue(depth + 1);
						} while (TryConsume(','));
						Expect(']');
						return;
					case (byte)'t':
						ValidateLiteral("true");
						return;
					case (byte)'f':
						ValidateLiteral("false");
						return;
					case (byte)'n':
						ValidateLiteral("null");
						return;
					default:
						ValidateNumber();
						return;
				}
			}

			private void EnterContainer(int depth)
			{
				if (depth == MaxPayloadDepth) {
					throw Error("it nests too deep");
				}
				_pos++;
			}

			private void ValidateLiteral(string literal)
			{
				foreach (var c in literal) {
					if (_pos >= _json.Length || _json[_pos] != c) {
						throw Error("a value is no JSON");
					}
					_pos++;
				}
			}

			private void ValidateNumber()
			{
				var start = _pos;
				if (_pos < _json.Length && _json[_pos] == '-') {
					_pos++;
				}
				// no leading zero in front of other digits
				if (_pos < _json.Length && _json[_pos] == '0') {
					_pos++;
				} else if (SkipDigits() == 0) {
					throw Error(_pos == start ? "a value is missing or no JSON" : "a number has no digits");
				}
				if (_pos < _json.Length && _json[_pos] == '.') {
					_pos++;
					if (SkipDigits() == 0) {
						throw Error("a number has no digits after its point");
					}
				}
				if (_pos < _json.Length && (_json[_pos] == 'e' || _json[_pos] == 'E')) {
					_pos++;
					if (_pos < _json.Length && (_json[_pos] == '+' || _json[_pos] == '-')) {
						_pos++;
					}
					if (SkipDigits() == 0) {
						throw Error("a number has no digits in its exponent");
					}
				}
				if (_pos < _json.Length && IsScalar(_json[_pos])) {
					throw Error("a number is no JSON");
				}
			}

			private int SkipDigits()
			{
				var start = _pos;
				while (_pos < _json.Length && _json[_pos] >= '0' && _json[_pos] <= '9') {
					_pos++;
				}
				return _pos - start;
			}

			private void ValidateString()
			{
				if (Peek() != '"') {
					throw Error("a string is missing");
				}
				for (_pos++; _pos < _json.Length; _pos++) {
					var c = _json[_pos];
					if (c == '"') {
						_pos++;
						return;
					}
					if (c < 0x20) {
						throw Error("a string contains a control character");
					}
					if (c != '\\') {
						continue;
					}
					if (++_pos >= _json.Length) {
						break;
					}
					switch (_json[_pos]) {
						case (byte)'"': case (byte)'\\': case (byte)'/': case (byte)'b': case (byte)'f': case (byte)'n': case (byte)'r': case (byte)'t':
							break;
						case (byte)'u':
							for (var i = 0; i < 4; i++) {
								if (++_pos >= _json.Length || !IsHex(_json[_pos])) {
									throw Error("a string has a broken \\u escape");
								}
							}
							break;
						default:
							throw Error("a string has an unknown escape");
					}
				}
				throw Error("it ends inside a string");
			}

			private static bool IsHex(byte c) => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';

			/// <summary>Moves past a string and says whether it contains escapes.</summary>
			private bool SkipString()
			{
				if (Peek() != '"') {
					throw Error("a string is missing");
				}
				var escaped = false;
				for (_pos++; _pos < _json.Length; _pos++) {
					var c = _json[_pos];
					if (c == '"') {
						_pos++;
						return escaped;
					}
					if (c == '\\') {
						escaped = true;
						_pos++;
					} else if (c < 0x20) {
						throw Error("a string contains a control character");
					}
				}
				throw Error("it ends inside a string");
			}

			private string Decode(int start, int length)
			{
				try {
					return Utf8.GetString(_json, start, length);
				} catch (ArgumentException) {
					throw Error("a string is not UTF-8");
				}
			}

			private void Expect(char expected)
			{
				SkipWhitespace();
				if (Peek() != expected) {
					throw Error($"'{expected}' is missing");
				}
				_pos++;
			}

			private bool TryConsume(char expected)
			{
				SkipWhitespace();
				if (_pos < _json.Length && _json[_pos] == expected) {
					_pos++;
					return true;
				}
				return false;
			}

			private byte Peek() => _pos < _json.Length ? _json[_pos] : throw Error("it ends early");

			private void SkipWhitespace()
			{
				while (_pos < _json.Length) {
					var c = _json[_pos];
					if (c != ' ' && c != '\n' && c != '\r' && c != '\t') {
						return;
					}
					_pos++;
				}
			}

			private static bool IsScalar(byte c) => c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c == '-' || c == '+' || c == '.';
		}
	}
}
