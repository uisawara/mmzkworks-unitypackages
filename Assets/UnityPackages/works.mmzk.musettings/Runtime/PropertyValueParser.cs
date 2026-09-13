using System;
using System.Globalization;
using Mmzkworks.muProperty;
using UnityEngine;

namespace Mmzkworks.muSettings
{
    /// <summary>
    /// Parses CLI / environment strings into typed <see cref="Mmzkworks.muProperty.PropertyValue"/> leaves.
    /// </summary>
    public static class PropertyValueParser
    {
        public static PropertyValue Parse(string raw)
        {
            if (raw == null)
            {
                return PropertyValue.From(string.Empty);
            }

            var s = raw.Trim();
            if (s.Length == 0)
            {
                return PropertyValue.From(string.Empty);
            }

            if (s.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return PropertyValue.From(true);
            }

            if (s.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return PropertyValue.From(false);
            }

            if (s[0] == '#' && TryParseHexColor(s, out var color))
            {
                return PropertyValue.From(color);
            }

            var inner = Unwrap(s);
            if (inner.IndexOf(',') >= 0 && TryParseVector(inner, out var vector))
            {
                return vector;
            }

            if (IsFloatToken(s))
            {
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    return PropertyValue.From(f);
                }
            }
            else if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                return PropertyValue.From(i);
            }
            else if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f2))
            {
                return PropertyValue.From(f2);
            }

            return PropertyValue.From(s);
        }

        public static bool TryParseHexColor(string s, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(s) || s[0] != '#')
            {
                return false;
            }

            var hex = s.Substring(1);
            if (hex.Length == 3)
            {
                if (!TryNibble(hex[0], out var r) || !TryNibble(hex[1], out var g) || !TryNibble(hex[2], out var b))
                {
                    return false;
                }

                color = new Color(r * 17 / 255f, g * 17 / 255f, b * 17 / 255f, 1f);
                return true;
            }

            if (hex.Length == 6)
            {
                if (!TryByte(hex, 0, out var r) || !TryByte(hex, 2, out var g) || !TryByte(hex, 4, out var b))
                {
                    return false;
                }

                color = new Color(r / 255f, g / 255f, b / 255f, 1f);
                return true;
            }

            if (hex.Length == 8)
            {
                if (!TryByte(hex, 0, out var r) || !TryByte(hex, 2, out var g) || !TryByte(hex, 4, out var b) || !TryByte(hex, 6, out var a))
                {
                    return false;
                }

                color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
                return true;
            }

            return false;
        }

        public static string ColorToHex(Color color)
        {
            var r = ToByte(color.r);
            var g = ToByte(color.g);
            var b = ToByte(color.b);
            var a = ToByte(color.a);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        static string Unwrap(string s)
        {
            if (s.Length >= 2)
            {
                var first = s[0];
                var last = s[s.Length - 1];
                if ((first == '(' && last == ')') || (first == '[' && last == ']'))
                {
                    return s.Substring(1, s.Length - 2).Trim();
                }
            }

            return s;
        }

        static bool TryParseVector(string inner, out PropertyValue value)
        {
            value = default;
            var parts = inner.Split(',');
            if (parts.Length < 2 || parts.Length > 4)
            {
                return false;
            }

            var nums = new float[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out nums[i]))
                {
                    return false;
                }
            }

            switch (parts.Length)
            {
                case 2:
                    value = PropertyValue.From(new Vector2(nums[0], nums[1]));
                    return true;
                case 3:
                    value = PropertyValue.From(new Vector3(nums[0], nums[1], nums[2]));
                    return true;
                default:
                    value = PropertyValue.From(new Vector4(nums[0], nums[1], nums[2], nums[3]));
                    return true;
            }
        }

        static bool IsFloatToken(string s)
        {
            return s.IndexOf('.') >= 0 || s.IndexOf('e') >= 0 || s.IndexOf('E') >= 0;
        }

        static bool TryNibble(char c, out int nibble)
        {
            if (c >= '0' && c <= '9')
            {
                nibble = c - '0';
                return true;
            }

            if (c >= 'a' && c <= 'f')
            {
                nibble = c - 'a' + 10;
                return true;
            }

            if (c >= 'A' && c <= 'F')
            {
                nibble = c - 'A' + 10;
                return true;
            }

            nibble = 0;
            return false;
        }

        static bool TryByte(string hex, int index, out byte value)
        {
            if (!TryNibble(hex[index], out var hi) || !TryNibble(hex[index + 1], out var lo))
            {
                value = 0;
                return false;
            }

            value = (byte)((hi << 4) | lo);
            return true;
        }

        static int ToByte(float channel)
        {
            return Mathf.Clamp(Mathf.RoundToInt(channel * 255f), 0, 255);
        }
    }
}
