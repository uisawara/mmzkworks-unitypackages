using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Minimal pretty-printer for the rules export and the validation report.
    /// Values are null, string, bool, a number, <see cref="JsonObject"/>, <see cref="JsonArray"/>, or object[].
    /// </summary>
    internal static class JsonText
    {
        public static string Stringify(object value)
        {
            var builder = new StringBuilder();
            Write(builder, value, 0);
            builder.Append('\n');
            return builder.ToString();
        }

        public static void Write(StringBuilder builder, object value, int indent)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    return;
                case string text:
                    builder.Append('"').Append(Escape(text)).Append('"');
                    return;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    return;
                case JsonObject obj:
                    obj.Write(builder, indent);
                    return;
                case JsonArray array:
                    array.Write(builder, indent);
                    return;
                case object[] items:
                    WriteList(builder, items, indent);
                    return;
                default:
                    if (TryFormatNumber(value, out var number))
                    {
                        builder.Append(number);
                        return;
                    }

                    builder.Append('"').Append(Escape(value.ToString())).Append('"');
                    return;
            }
        }

        public static void WriteList(StringBuilder builder, IList items, int indent)
        {
            if (items == null || items.Count == 0)
            {
                builder.Append("[]");
                return;
            }

            builder.Append("[\n");
            for (var i = 0; i < items.Count; i++)
            {
                Indent(builder, indent + 1);
                Write(builder, items[i], indent + 1);
                if (i + 1 < items.Count) builder.Append(',');
                builder.Append('\n');
            }

            Indent(builder, indent);
            builder.Append(']');
        }

        public static void Indent(StringBuilder builder, int indent)
        {
            builder.Append(' ', indent * 2);
        }

        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? "";

            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    default:
                        if (character < ' ') builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }

        private static bool TryFormatNumber(object value, out string text)
        {
            text = null;
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    text = Convert.ToString(value, CultureInfo.InvariantCulture);
                    return true;
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    text = double.IsNaN(number) || double.IsInfinity(number)
                        ? "null"
                        : number.ToString("G", CultureInfo.InvariantCulture);
                    return true;
                default:
                    return false;
            }
        }
    }

    internal sealed class JsonObject
    {
        private readonly List<string> _names = new List<string>();
        private readonly List<object> _values = new List<object>();

        public void Set(string name, object value)
        {
            _names.Add(name);
            _values.Add(value);
        }

        public void Write(StringBuilder builder, int indent)
        {
            if (_names.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            builder.Append("{\n");
            for (var i = 0; i < _names.Count; i++)
            {
                JsonText.Indent(builder, indent + 1);
                builder.Append('"').Append(JsonText.Escape(_names[i])).Append("\": ");
                JsonText.Write(builder, _values[i], indent + 1);
                if (i + 1 < _names.Count) builder.Append(',');
                builder.Append('\n');
            }

            JsonText.Indent(builder, indent);
            builder.Append('}');
        }

        public override string ToString() => JsonText.Stringify(this);
    }

    internal sealed class JsonArray
    {
        private readonly List<object> _items = new List<object>();

        public void Add(object value) => _items.Add(value);

        public void Write(StringBuilder builder, int indent)
        {
            JsonText.WriteList(builder, _items, indent);
        }

        public override string ToString() => JsonText.Stringify(this);
    }
}
