using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Convierte entre valores normales de C# y el formato tipado de la API REST de Firestore, por ejemplo
/// <c>{"totalScore": {"integerValue": "120"}}</c>.
/// </summary>
public static class FirestoreConvert
{
    // ---------------------------------------------------------------- C# -> Firestore

    /// <summary>Diccionario plano de C# -> objeto "fields" de Firestore.</summary>
    public static Dictionary<string, object> ToFields(IDictionary<string, object> data)
    {
        var fields = new Dictionary<string, object>();
        foreach (var kv in data) fields[kv.Key] = ToValue(kv.Value);
        return fields;
    }

    public static Dictionary<string, object> ToValue(object value)
    {
        switch (value)
        {
            case null: return new Dictionary<string, object> { ["nullValue"] = null };
            case bool b: return new Dictionary<string, object> { ["booleanValue"] = b };
            case int n: return new Dictionary<string, object> { ["integerValue"] = n.ToString(CultureInfo.InvariantCulture) };
            case long n: return new Dictionary<string, object> { ["integerValue"] = n.ToString(CultureInfo.InvariantCulture) };
            case float f: return new Dictionary<string, object> { ["doubleValue"] = (double)f };
            case double d: return new Dictionary<string, object> { ["doubleValue"] = d };
            case string s: return new Dictionary<string, object> { ["stringValue"] = s };
            case DateTime dt:
                return new Dictionary<string, object>
                {
                    ["timestampValue"] = dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)
                };
            case IDictionary<string, object> map:
                return new Dictionary<string, object>
                {
                    ["mapValue"] = new Dictionary<string, object> { ["fields"] = ToFields(map) }
                };
            case IEnumerable list:
                var values = new List<object>();
                foreach (object item in list) values.Add(ToValue(item));
                return new Dictionary<string, object>
                {
                    ["arrayValue"] = new Dictionary<string, object> { ["values"] = values }
                };
            default:
                return new Dictionary<string, object> { ["stringValue"] = Convert.ToString(value, CultureInfo.InvariantCulture) };
        }
    }

    // ---------------------------------------------------------------- Firestore -> C#

    /// <summary>Objeto "fields" de Firestore -> diccionario plano de C#.</summary>
    public static Dictionary<string, object> FromFields(Dictionary<string, object> fields)
    {
        var data = new Dictionary<string, object>();
        if (fields == null) return data;
        foreach (var kv in fields) data[kv.Key] = FromValue(kv.Value as Dictionary<string, object>);
        return data;
    }

    public static object FromValue(Dictionary<string, object> v)
    {
        if (v == null) return null;
        if (v.TryGetValue("integerValue", out object iv)) return long.Parse(iv.ToString(), CultureInfo.InvariantCulture);
        if (v.TryGetValue("doubleValue", out object dv)) return Convert.ToDouble(dv, CultureInfo.InvariantCulture);
        if (v.TryGetValue("stringValue", out object sv)) return sv as string;
        if (v.TryGetValue("booleanValue", out object bv)) return bv is bool b && b;
        if (v.TryGetValue("timestampValue", out object tv)) return tv as string;
        if (v.ContainsKey("nullValue")) return null;
        if (v.TryGetValue("mapValue", out object mv))
        {
            var map = mv as Dictionary<string, object>;
            return FromFields(map != null && map.TryGetValue("fields", out object f) ? f as Dictionary<string, object> : null);
        }
        if (v.TryGetValue("arrayValue", out object av))
        {
            var list = new List<object>();
            if (av is Dictionary<string, object> arr && arr.TryGetValue("values", out object vals) && vals is List<object> items)
                foreach (object item in items) list.Add(FromValue(item as Dictionary<string, object>));
            return list;
        }
        return null;
    }

    /// <summary>Lee los campos de un documento de Firestore (<c>{"name": ..., "fields": {...}}</c>).</summary>
    public static Dictionary<string, object> DocumentData(Dictionary<string, object> document)
    {
        if (document == null) return new Dictionary<string, object>();
        return FromFields(document.TryGetValue("fields", out object f) ? f as Dictionary<string, object> : null);
    }

    /// <summary>Id del documento a partir de su "name" (ultimo segmento de la ruta).</summary>
    public static string DocumentId(Dictionary<string, object> document)
    {
        if (document == null || !document.TryGetValue("name", out object n) || !(n is string name)) return null;
        int slash = name.LastIndexOf('/');
        return slash >= 0 ? name.Substring(slash + 1) : name;
    }

    // ---------------------------------------------------------------- lectura segura

    public static int GetInt(IDictionary<string, object> d, string key, int fallback = 0)
    {
        if (d == null || !d.TryGetValue(key, out object v) || v == null) return fallback;
        try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
        catch { return fallback; }
    }

    public static float GetFloat(IDictionary<string, object> d, string key, float fallback = 0f)
    {
        if (d == null || !d.TryGetValue(key, out object v) || v == null) return fallback;
        try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); }
        catch { return fallback; }
    }

    public static string GetString(IDictionary<string, object> d, string key, string fallback = "")
    {
        if (d == null || !d.TryGetValue(key, out object v) || v == null) return fallback;
        return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    public static bool GetBool(IDictionary<string, object> d, string key, bool fallback = false)
    {
        if (d == null || !d.TryGetValue(key, out object v) || v == null) return fallback;
        return v is bool b ? b : fallback;
    }

    public static Dictionary<string, object> GetMap(IDictionary<string, object> d, string key)
    {
        if (d == null || !d.TryGetValue(key, out object v)) return null;
        return v as Dictionary<string, object>;
    }
}
