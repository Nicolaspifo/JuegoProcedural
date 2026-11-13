using UnityEngine;

/// <summary>
/// Configuracion de Firebase. El asset vive en <c>Assets/_Project/Resources/FirebaseSettings.asset</c> y se
/// carga solo. Los datos salen de la consola de Firebase: Configuracion del proyecto > General.
/// La Web API Key NO es secreta (identifica el proyecto); la seguridad la ponen las reglas de Firestore.
/// </summary>
[CreateAssetMenu(fileName = "FirebaseSettings", menuName = "A Punta de Machete/Firebase Settings")]
public class FirebaseSettings : ScriptableObject
{
    public const string ResourcePath = "FirebaseSettings";

    [Header("Proyecto de Firebase")]
    [Tooltip("Configuracion del proyecto > General > Clave de API web.")]
    public string webApiKey = "";
    [Tooltip("Configuracion del proyecto > General > ID del proyecto.")]
    public string projectId = "";

    [Header("Modo de prueba")]
    [Tooltip("Usar datos falsos locales en vez de Firebase (para disenar pantallas sin internet).")]
    public bool useMock = true;
    [Tooltip("Retraso simulado de cada llamada del Mock, en segundos.")]
    [Min(0f)] public float mockDelay = 0.6f;
    [Tooltip("Probabilidad (0-1) de que el Mock simule un error de red, para probar los estados de error.")]
    [Range(0f, 1f)] public float mockNetworkErrorChance = 0f;

    [Header("Red")]
    [Min(3)] public int requestTimeoutSeconds = 15;
    [Tooltip("Escribe en consola cada peticion (util para las evidencias).")]
    public bool logRequests = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(webApiKey) && !string.IsNullOrWhiteSpace(projectId);

    public static FirebaseSettings Load() => Resources.Load<FirebaseSettings>(ResourcePath);
}
