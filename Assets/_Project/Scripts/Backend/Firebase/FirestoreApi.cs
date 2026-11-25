using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Cloud Firestore por REST. Documentacion: https://firebase.google.com/docs/firestore/use-rest-api
/// Cada metodo recibe el IdToken del usuario: las reglas de Firestore lo usan para saber quien escribe.
/// </summary>
public class FirestoreApi
{
    private readonly string documentsUrl;   // .../projects/{id}/databases/(default)/documents

    public FirestoreApi(string projectId)
    {
        documentsUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents";
    }

    /// <summary>Lee un documento. Ok con Data = null si no existe.</summary>
    public async Task<Result<Dictionary<string, object>>> GetDocument(string path, string idToken)
    {
        HttpResponse r = await HttpClientUnity.SendJson("GET", $"{documentsUrl}/{path}", null, idToken);
        if (r.Code == 404 && !r.Text.Contains("does not exist for project"))
            return Result<Dictionary<string, object>>.Success(null);   // el documento no existe todavia
        if (!r.Ok) return FirebaseErrors.ToResult<Dictionary<string, object>>(r);
        return Result<Dictionary<string, object>>.Success(FirestoreConvert.DocumentData(MiniJson.DeserializeObject(r.Text)));
    }

    /// <summary>Crea o reemplaza el documento completo con estos campos.</summary>
    public async Task<Result> SetDocument(string path, IDictionary<string, object> data, string idToken)
    {
        string body = MiniJson.Serialize(new Dictionary<string, object> { ["fields"] = FirestoreConvert.ToFields(data) });
        HttpResponse r = await HttpClientUnity.SendJson("PATCH", $"{documentsUrl}/{path}", body, idToken);
        return r.Ok ? Result.Success() : FirebaseErrors.ToResult(r);
    }

    /// <summary>
    /// Actualiza SOLO los campos indicados (los demas se conservan, por ejemplo createdAt). Si el documento no
    /// existe, lo crea.
    /// </summary>
    public async Task<Result> MergeDocument(string path, IDictionary<string, object> data, string idToken)
    {
        var mask = new System.Text.StringBuilder();
        foreach (string key in data.Keys)
            mask.Append(mask.Length == 0 ? "?" : "&").Append("updateMask.fieldPaths=").Append(UnityEngine.Networking.UnityWebRequest.EscapeURL(key));

        string body = MiniJson.Serialize(new Dictionary<string, object> { ["fields"] = FirestoreConvert.ToFields(data) });
        HttpResponse r = await HttpClientUnity.SendJson("PATCH", $"{documentsUrl}/{path}{mask}", body, idToken);
        return r.Ok ? Result.Success() : FirebaseErrors.ToResult(r);
    }

    /// <summary>Agrega un documento con id automatico a una coleccion (por ejemplo el historial de partidas).</summary>
    public async Task<Result> AddDocument(string collectionPath, IDictionary<string, object> data, string idToken)
    {
        string body = MiniJson.Serialize(new Dictionary<string, object> { ["fields"] = FirestoreConvert.ToFields(data) });
        HttpResponse r = await HttpClientUnity.SendJson("POST", $"{documentsUrl}/{collectionPath}", body, idToken);
        return r.Ok ? Result.Success() : FirebaseErrors.ToResult(r);
    }

    /// <summary>
    /// Consulta los documentos de una coleccion de primer nivel ordenados por un campo, de mayor a menor.
    /// Devuelve (id, datos) de cada documento.
    /// </summary>
    public async Task<Result<List<KeyValuePair<string, Dictionary<string, object>>>>> QueryTop(
        string collectionId, string orderByField, int limit, string idToken)
    {
        var query = new Dictionary<string, object>
        {
            ["structuredQuery"] = new Dictionary<string, object>
            {
                ["from"] = new List<object> { new Dictionary<string, object> { ["collectionId"] = collectionId } },
                ["orderBy"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["field"] = new Dictionary<string, object> { ["fieldPath"] = orderByField },
                        ["direction"] = "DESCENDING"
                    }
                },
                ["limit"] = limit
            }
        };

        HttpResponse r = await HttpClientUnity.SendJson("POST", documentsUrl + ":runQuery", MiniJson.Serialize(query), idToken);
        if (!r.Ok) return FirebaseErrors.ToResult<List<KeyValuePair<string, Dictionary<string, object>>>>(r);

        var rows = new List<KeyValuePair<string, Dictionary<string, object>>>();
        if (MiniJson.Deserialize(r.Text) is List<object> items)
        {
            foreach (object item in items)
            {
                // Cada elemento es {"document": {...}, "readTime": ...}; si no hay resultados viene sin "document".
                if (item is Dictionary<string, object> row && row.TryGetValue("document", out object d) && d is Dictionary<string, object> doc)
                    rows.Add(new KeyValuePair<string, Dictionary<string, object>>(
                        FirestoreConvert.DocumentId(doc), FirestoreConvert.DocumentData(doc)));
            }
        }
        return Result<List<KeyValuePair<string, Dictionary<string, object>>>>.Success(rows);
    }

    /// <summary>Cuenta cuantos documentos tienen <paramref name="field"/> mayor que <paramref name="value"/>.</summary>
    public async Task<Result<int>> CountGreaterThan(string collectionId, string field, long value, string idToken)
    {
        var query = new Dictionary<string, object>
        {
            ["structuredAggregationQuery"] = new Dictionary<string, object>
            {
                ["structuredQuery"] = new Dictionary<string, object>
                {
                    ["from"] = new List<object> { new Dictionary<string, object> { ["collectionId"] = collectionId } },
                    ["where"] = new Dictionary<string, object>
                    {
                        ["fieldFilter"] = new Dictionary<string, object>
                        {
                            ["field"] = new Dictionary<string, object> { ["fieldPath"] = field },
                            ["op"] = "GREATER_THAN",
                            ["value"] = FirestoreConvert.ToValue(value)
                        }
                    }
                },
                ["aggregations"] = new List<object>
                {
                    new Dictionary<string, object> { ["alias"] = "total", ["count"] = new Dictionary<string, object>() }
                }
            }
        };

        HttpResponse r = await HttpClientUnity.SendJson("POST", documentsUrl + ":runAggregationQuery", MiniJson.Serialize(query), idToken);
        if (!r.Ok) return FirebaseErrors.ToResult<int>(r);

        // [{"result": {"aggregateFields": {"total": {"integerValue": "5"}}}, "readTime": "..."}]
        if (MiniJson.Deserialize(r.Text) is List<object> items && items.Count > 0 &&
            items[0] is Dictionary<string, object> row &&
            FirestoreConvert.GetMap(row, "result") is Dictionary<string, object> result &&
            FirestoreConvert.GetMap(result, "aggregateFields") is Dictionary<string, object> fields)
        {
            var data = FirestoreConvert.FromFields(fields);
            return Result<int>.Success(FirestoreConvert.GetInt(data, "total"));
        }
        return Result<int>.Success(0);
    }
}
