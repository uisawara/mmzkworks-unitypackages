using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Mmzkworks.muProperty;
using UnityEngine;

namespace Mmzkworks.muSettings
{
    internal static class JsonPropertyTreeCodec
    {
        public static PropertyTree Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (json.Length > 0 && json[0] == '\uFEFF')
            {
                json = json.Substring(1);
            }

            var parser = new Parser(json);
            var root = parser.ParseValue();
            parser.SkipWs();
            if (!parser.Eof)
            {
                throw parser.Error("Trailing content after JSON value.");
            }

            if (!(root is Dictionary<string, object> obj))
            {
                throw new FormatException("JSON root must be an object.");
            }

            return FromObject(obj);
        }

        public static string ToJson(PropertyTree tree)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            var writer = new Writer();
            writer.WriteTree(tree, 0);
            writer.Builder.Append('\n');
            return writer.Builder.ToString();
        }

        static PropertyTree FromObject(Dictionary<string, object> obj)
        {
            var tree = new PropertyTree();
            foreach (var pair in obj)
            {
                Assign(tree, pair.Key, pair.Value);
            }

            return tree;
        }

        static void Assign(PropertyTree tree, string key, object token)
        {
            if (token == null)
            {
                return;
            }

            if (token is bool b)
            {
                tree.Set(key, b);
                return;
            }

            if (token is int i)
            {
                tree.Set(key, i);
                return;
            }

            if (token is float f)
            {
                tree.Set(key, f);
                return;
            }

            if (token is string s)
            {
                if (PropertyValueParser.TryParseHexColor(s, out var color))
                {
                    tree.Set(key, color);
                }
                else
                {
                    tree.Set(key, s);
                }

                return;
            }

            if (token is List<object> list)
            {
                tree.Set(key, VectorFromArray(list));
                return;
            }

            if (token is Dictionary<string, object> child)
            {
                if (TryAsColor(child, out var color))
                {
                    tree.Set(key, color);
                    return;
                }

                if (TryAsVector(child, out var vector))
                {
                    tree.Set(key, vector);
                    return;
                }

                tree.Set(key, FromObject(child));
            }
        }

        static PropertyValue VectorFromArray(List<object> list)
        {
            if (list.Count < 2 || list.Count > 4)
            {
                throw new FormatException("JSON arrays must have 2, 3, or 4 numbers (Vector2/3/4).");
            }

            var nums = new float[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                nums[i] = ToFloat(list[i]);
            }

            switch (list.Count)
            {
                case 2:
                    return PropertyValue.From(new Vector2(nums[0], nums[1]));
                case 3:
                    return PropertyValue.From(new Vector3(nums[0], nums[1], nums[2]));
                default:
                    return PropertyValue.From(new Vector4(nums[0], nums[1], nums[2], nums[3]));
            }
        }

        static bool TryAsColor(Dictionary<string, object> obj, out Color color)
        {
            color = default;
            if (!obj.ContainsKey("r") || !obj.ContainsKey("g") || !obj.ContainsKey("b"))
            {
                return false;
            }

            foreach (var key in obj.Keys)
            {
                if (key != "r" && key != "g" && key != "b" && key != "a")
                {
                    return false;
                }
            }

            try
            {
                var a = obj.ContainsKey("a") ? ToFloat(obj["a"]) : 1f;
                color = new Color(ToFloat(obj["r"]), ToFloat(obj["g"]), ToFloat(obj["b"]), a);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        static bool TryAsVector(Dictionary<string, object> obj, out PropertyValue value)
        {
            value = default;
            var hasX = obj.ContainsKey("x");
            var hasY = obj.ContainsKey("y");
            var hasZ = obj.ContainsKey("z");
            var hasW = obj.ContainsKey("w");
            if (!hasX || !hasY)
            {
                return false;
            }

            foreach (var key in obj.Keys)
            {
                if (key != "x" && key != "y" && key != "z" && key != "w")
                {
                    return false;
                }
            }

            try
            {
                var x = ToFloat(obj["x"]);
                var y = ToFloat(obj["y"]);
                if (hasW)
                {
                    if (!hasZ)
                    {
                        return false;
                    }

                    value = PropertyValue.From(new Vector4(x, y, ToFloat(obj["z"]), ToFloat(obj["w"])));
                    return true;
                }

                if (hasZ)
                {
                    value = PropertyValue.From(new Vector3(x, y, ToFloat(obj["z"])));
                    return true;
                }

                value = PropertyValue.From(new Vector2(x, y));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        static float ToFloat(object token)
        {
            if (token is int i)
            {
                return i;
            }

            if (token is float f)
            {
                return f;
            }

            throw new FormatException("Expected a number.");
        }

        sealed class Parser
        {
            readonly string _text;
            int _index;

            public Parser(string text)
            {
                _text = text;
            }

            public bool Eof => _index >= _text.Length;

            public object ParseValue()
            {
                SkipWs();
                if (Eof)
                {
                    throw Error("Unexpected end of JSON.");
                }

                var c = _text[_index];
                switch (c)
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return ParseString();
                    case 't':
                        Expect("true");
                        return true;
                    case 'f':
                        Expect("false");
                        return false;
                    case 'n':
                        Expect("null");
                        return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            return ParseNumber();
                        }

                        throw Error($"Unexpected character '{c}'.");
                }
            }

            public void SkipWs()
            {
                while (_index < _text.Length)
                {
                    var c = _text[_index];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    {
                        return;
                    }

                    _index++;
                }
            }

            public FormatException Error(string message)
            {
                return new FormatException($"{message} At index {_index}.");
            }

            Dictionary<string, object> ParseObject()
            {
                _index++;
                var obj = new Dictionary<string, object>();
                SkipWs();
                if (Peek('}'))
                {
                    _index++;
                    return obj;
                }

                while (true)
                {
                    SkipWs();
                    if (Eof || _text[_index] != '"')
                    {
                        throw Error("Expected object key.");
                    }

                    var key = ParseString();
                    SkipWs();
                    if (!Peek(':'))
                    {
                        throw Error("Expected ':' after object key.");
                    }

                    _index++;
                    obj[key] = ParseValue();
                    SkipWs();
                    if (Peek('}'))
                    {
                        _index++;
                        return obj;
                    }

                    if (!Peek(','))
                    {
                        throw Error("Expected ',' or '}' in object.");
                    }

                    _index++;
                }
            }

            List<object> ParseArray()
            {
                _index++;
                var list = new List<object>();
                SkipWs();
                if (Peek(']'))
                {
                    _index++;
                    return list;
                }

                while (true)
                {
                    list.Add(ParseValue());
                    SkipWs();
                    if (Peek(']'))
                    {
                        _index++;
                        return list;
                    }

                    if (!Peek(','))
                    {
                        throw Error("Expected ',' or ']' in array.");
                    }

                    _index++;
                }
            }

            string ParseString()
            {
                _index++;
                var sb = new StringBuilder();
                while (_index < _text.Length)
                {
                    var c = _text[_index++];
                    if (c == '"')
                    {
                        return sb.ToString();
                    }

                    if (c == '\\')
                    {
                        if (_index >= _text.Length)
                        {
                            throw Error("Unterminated escape.");
                        }

                        var e = _text[_index++];
                        switch (e)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                sb.Append(e);
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 'f':
                                sb.Append('\f');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                            case 'u':
                                if (_index + 4 > _text.Length)
                                {
                                    throw Error("Invalid unicode escape.");
                                }

                                var hex = _text.Substring(_index, 4);
                                _index += 4;
                                if (!ushort.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
                                {
                                    throw Error("Invalid unicode escape.");
                                }

                                sb.Append((char)code);
                                break;
                            default:
                                throw Error($"Invalid escape '\\{e}'.");
                        }

                        continue;
                    }

                    if (c < 0x20)
                    {
                        throw Error("Unescaped control character in string.");
                    }

                    sb.Append(c);
                }

                throw Error("Unterminated string.");
            }

            object ParseNumber()
            {
                var start = _index;
                if (Peek('-'))
                {
                    _index++;
                }

                if (Eof)
                {
                    throw Error("Invalid number.");
                }

                if (_text[_index] == '0')
                {
                    _index++;
                }
                else if (_text[_index] >= '1' && _text[_index] <= '9')
                {
                    while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
                    {
                        _index++;
                    }
                }
                else
                {
                    throw Error("Invalid number.");
                }

                var isFloat = false;
                if (Peek('.'))
                {
                    isFloat = true;
                    _index++;
                    var digit = false;
                    while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
                    {
                        digit = true;
                        _index++;
                    }

                    if (!digit)
                    {
                        throw Error("Invalid number.");
                    }
                }

                if (Peek('e') || Peek('E'))
                {
                    isFloat = true;
                    _index++;
                    if (Peek('+') || Peek('-'))
                    {
                        _index++;
                    }

                    var digit = false;
                    while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
                    {
                        digit = true;
                        _index++;
                    }

                    if (!digit)
                    {
                        throw Error("Invalid number.");
                    }
                }

                var token = _text.Substring(start, _index - start);
                if (!isFloat && int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                {
                    return i;
                }

                if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    return f;
                }

                throw Error("Invalid number.");
            }

            void Expect(string word)
            {
                if (_index + word.Length > _text.Length || _text.Substring(_index, word.Length) != word)
                {
                    throw Error($"Expected '{word}'.");
                }

                _index += word.Length;
            }

            bool Peek(char c)
            {
                return _index < _text.Length && _text[_index] == c;
            }
        }

        sealed class Writer
        {
            public StringBuilder Builder { get; } = new StringBuilder();

            public void WriteTree(PropertyTree tree, int indent)
            {
                Builder.Append('{');
                var keys = new List<string>(tree.Keys);
                if (keys.Count == 0)
                {
                    Builder.Append('}');
                    return;
                }

                Builder.Append('\n');
                for (var i = 0; i < keys.Count; i++)
                {
                    WriteIndent(indent + 1);
                    WriteString(keys[i]);
                    Builder.Append(": ");
                    WriteChild(tree, keys[i], indent + 1);
                    if (i < keys.Count - 1)
                    {
                        Builder.Append(',');
                    }

                    Builder.Append('\n');
                }

                WriteIndent(indent);
                Builder.Append('}');
            }

            void WriteChild(PropertyTree tree, string key, int indent)
            {
                if (tree.TryGet(key, out var value))
                {
                    WriteValue(value);
                    return;
                }

                if (tree.TryGetObject(key, out var child))
                {
                    WriteTree(child, indent);
                }
            }

            void WriteValue(PropertyValue value)
            {
                switch (value.Kind)
                {
                    case PropertyValueKind.Bool:
                        Builder.Append(value.AsBool() ? "true" : "false");
                        break;
                    case PropertyValueKind.Int:
                        Builder.Append(value.AsInt().ToString(CultureInfo.InvariantCulture));
                        break;
                    case PropertyValueKind.Float:
                        Builder.Append(FormatFloat(value.AsFloat()));
                        break;
                    case PropertyValueKind.Vector2:
                    {
                        var v = value.AsVector2();
                        WriteVector(v.x, v.y);
                        break;
                    }
                    case PropertyValueKind.Vector3:
                    {
                        var v = value.AsVector3();
                        WriteVector(v.x, v.y, v.z);
                        break;
                    }
                    case PropertyValueKind.Vector4:
                    {
                        var v = value.AsVector4();
                        WriteVector(v.x, v.y, v.z, v.w);
                        break;
                    }
                    case PropertyValueKind.Color:
                        WriteString(PropertyValueParser.ColorToHex(value.AsColor()));
                        break;
                    case PropertyValueKind.String:
                        WriteString(value.AsString());
                        break;
                }
            }

            void WriteVector(params float[] components)
            {
                Builder.Append('[');
                for (var i = 0; i < components.Length; i++)
                {
                    if (i > 0)
                    {
                        Builder.Append(", ");
                    }

                    Builder.Append(FormatFloat(components[i]));
                }

                Builder.Append(']');
            }

            void WriteString(string s)
            {
                Builder.Append('"');
                foreach (var c in s)
                {
                    switch (c)
                    {
                        case '"':
                            Builder.Append("\\\"");
                            break;
                        case '\\':
                            Builder.Append("\\\\");
                            break;
                        case '\b':
                            Builder.Append("\\b");
                            break;
                        case '\f':
                            Builder.Append("\\f");
                            break;
                        case '\n':
                            Builder.Append("\\n");
                            break;
                        case '\r':
                            Builder.Append("\\r");
                            break;
                        case '\t':
                            Builder.Append("\\t");
                            break;
                        default:
                            if (c < 0x20)
                            {
                                Builder.Append("\\u");
                                Builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                Builder.Append(c);
                            }

                            break;
                    }
                }

                Builder.Append('"');
            }

            void WriteIndent(int indent)
            {
                Builder.Append(' ', indent * 2);
            }

            static string FormatFloat(float value)
            {
                var s = value.ToString("G9", CultureInfo.InvariantCulture);
                if (s.IndexOf('.') < 0 && s.IndexOf('e') < 0 && s.IndexOf('E') < 0)
                {
                    s += ".0";
                }

                return s;
            }
        }
    }
}
