using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// JSON minimo (JsonUtility de Unity no soporta diccionarios ni arrays en la raiz, que es justo lo que devuelve
/// la API REST de Firebase).
/// Deserialize: objetos -> Dictionary&lt;string, object&gt;, arrays -> List&lt;object&gt;, numeros enteros -> long,
/// decimales -> double, true/false -> bool, null -> null.
/// Serialize: acepta diccionarios, listas, string, bool, numeros y null.
/// </summary>
public static class MiniJson
{
    public static object Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var parser = new Parser(json);
        object value = parser.ParseValue();
        return value;
    }

    /// <summary>Atajo: deserializa y devuelve el objeto raiz como diccionario (o null).</summary>
    public static Dictionary<string, object> DeserializeObject(string json) =>
        Deserialize(json) as Dictionary<string, object>;

    public static string Serialize(object value)
    {
        var sb = new StringBuilder(256);
        Write(sb, value);
        return sb.ToString();
    }

    // ---------------------------------------------------------------- lectura

    private class Parser
    {
        private readonly string s;
        private int i;

        public Parser(string json) { s = json; i = 0; }

        public object ParseValue()
        {
            SkipWhitespace();
            if (i >= s.Length) throw new FormatException("JSON incompleto");

            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return ParseString();
                case 't': Expect("true"); return true;
                case 'f': Expect("false"); return false;
                case 'n': Expect("null"); return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                    throw new FormatException($"Caracter inesperado '{c}' en la posicion {i}");
            }
        }

        private Dictionary<string, object> ParseObject()
        {
            var dict = new Dictionary<string, object>();
            i++; // {
            SkipWhitespace();
            if (Peek() == '}') { i++; return dict; }

            while (true)
            {
                SkipWhitespace();
                string key = ParseString();
                SkipWhitespace();
                if (Next() != ':') throw new FormatException("Se esperaba ':'");
                dict[key] = ParseValue();
                SkipWhitespace();
                char c = Next();
                if (c == '}') return dict;
                if (c != ',') throw new FormatException("Se esperaba ',' o '}'");
            }
        }

        private List<object> ParseArray()
        {
            var list = new List<object>();
            i++; // [
            SkipWhitespace();
            if (Peek() == ']') { i++; return list; }

            while (true)
            {
                list.Add(ParseValue());
                SkipWhitespace();
                char c = Next();
                if (c == ']') return list;
                if (c != ',') throw new FormatException("Se esperaba ',' o ']'");
            }
        }

        private string ParseString()
        {
            if (Next() != '"') throw new FormatException("Se esperaba '\"'");
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("Escape \\u incompleto");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("Texto sin cerrar");
        }

        private object ParseNumber()
        {
            int start = i;
            bool isDecimal = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '.' || c == 'e' || c == 'E') isDecimal = true;
                else if (!(c == '-' || c == '+' || (c >= '0' && c <= '9'))) break;
                i++;
            }
            string num = s.Substring(start, i - start);
            if (!isDecimal && long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
            return double.Parse(num, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private void Expect(string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                throw new FormatException($"Se esperaba '{word}' en la posicion {i}");
            i += word.Length;
        }

        private void SkipWhitespace()
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private char Peek() => i < s.Length ? s[i] : '\0';
        private char Next() => i < s.Length ? s[i++] : '\0';
    }

    // ---------------------------------------------------------------- escritura

    private static void Write(StringBuilder sb, object value)
    {
        switch (value)
        {
            case null: sb.Append("null"); break;
            case string str: WriteString(sb, str); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
            case float f: sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
            case IDictionary dict:
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key.ToString());
                    sb.Append(':');
                    Write(sb, kv.Value);
                }
                sb.Append('}');
                break;
            case IEnumerable list:
                sb.Append('[');
                bool firstItem = true;
                foreach (object item in list)
                {
                    if (!firstItem) sb.Append(',');
                    firstItem = false;
                    Write(sb, item);
                }
                sb.Append(']');
                break;
            default:
                WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static void WriteString(StringBuilder sb, string str)
    {
        sb.Append('"');
        foreach (char c in str)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
