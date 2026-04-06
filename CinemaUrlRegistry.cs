using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Data;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
#endif

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CinemaUrlRegistry : UdonSharpBehaviour
{
    [Header("Debug")]
    public bool enableDebugLogs = true;

    [Header("Security")]
    [Tooltip("Tu servidor de API (Ej. https://micinema-api.onrender.com)")]
    public string serverBaseUrl = "https://micinema-api.onrender.com";
    [Tooltip("Link de toque (Opcional): https://micinema-api.onrender.com/knock")]
    public VRCUrl knockUrl;

    [Header("Keys")]
    public string[] ids;

    [Header("Video URLs")]
    public VRCUrl[] mainUrls;
    public VRCUrl[] altUrls;

    [Header("Poster URLs")]
    public VRCUrl[] posterPcUrls;
    public VRCUrl[] posterQuestUrls;

    [Header("Hero / Backdrop URLs (opcional)")]
    public VRCUrl[] heroPcUrls;
    public VRCUrl[] heroQuestUrls;

    private DataDictionary _idToIndex;
    private bool _initialized;

    private void Log(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log("[CinemaUrlRegistry] " + message);
    }

    public void Start()
    {
        ValidateArrays();
    }

    private void ValidateArrays()
    {
        if (ids == null) return;
        int count = ids.Length;
        
        CheckArray("mainUrls", mainUrls, count);
        CheckArray("altUrls", altUrls, count);
        CheckArray("posterPcUrls", posterPcUrls, count);
        CheckArray("posterQuestUrls", posterQuestUrls, count);
        CheckArray("heroPcUrls", heroPcUrls, count);
        CheckArray("heroQuestUrls", heroQuestUrls, count);
    }

    private void CheckArray(string name, VRCUrl[] array, int expected)
    {
        if (array == null) return;
        if (array.Length != expected)
        {
            LogError("INCONSISTENCIA: El array '" + name + "' tiene " + array.Length + " elementos, pero 'ids' tiene " + expected + ". Esto causará errores de reproducción.");
        }
    }

    private void LogError(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogError("[CinemaUrlRegistry] " + message);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogWarning("[CinemaUrlRegistry] " + message);
    }

    public int FindIndexById(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        
        if (!_initialized) InitializeCache();

        if (_idToIndex != null && _idToIndex.ContainsKey(id))
        {
            DataToken indexToken;
            if (_idToIndex.TryGetValue(id, out indexToken))
            {
                return indexToken.Int;
            }
        }

        return -1;
    }

    private void InitializeCache()
    {
        if (_initialized) return;
        _initialized = true;

        if (ids == null)
        {
            _idToIndex = new DataDictionary();
            return;
        }

        int count = ids.Length;
        _idToIndex = new DataDictionary();
        for (int i = 0; i < count; i++)
        {
            string key = ids[i];
            if (string.IsNullOrEmpty(key)) continue;
            
            if (!_idToIndex.ContainsKey(key))
            {
                _idToIndex.Add(key, i);
            }
            else
            {
                LogWarning("ID duplicado detectado en el Registro: '" + key + "' en el índice " + i);
            }
        }
        
        Log("Cache del Registro inicializado con " + _idToIndex.Count + " entradas.");
    }

    public bool HasEntry(string id)
    {
        return FindIndexById(id) >= 0;
    }

    public VRCUrl GetMainUrlById(string id)
    {
        return GetUrlSafe(mainUrls, FindIndexById(id));
    }

    public VRCUrl GetAltUrlById(string id)
    {
        return GetUrlSafe(altUrls, FindIndexById(id));
    }

    public VRCUrl GetPosterUrlById(string id, bool questBuild)
    {
        int index = FindIndexById(id);
        if (index < 0)
        {
            LogWarning("GetPosterUrlById | id no encontrado='" + id + "'.");
            return VRCUrl.Empty;
        }
        if (questBuild)
        {
            VRCUrl quest = GetUrlSafe(posterQuestUrls, index);
            if (!VRCUrl.IsNullOrEmpty(quest)) return quest;
        }

        VRCUrl url = GetUrlSafe(posterPcUrls, index);
        Log("GetPosterUrlById | id='" + id + "' | questBuild=" + questBuild + " | url=" + (url == null ? "null" : url.Get()));
        return url;
    }

    public VRCUrl GetHeroUrlById(string id, bool questBuild)
    {
        int index = FindIndexById(id);
        if (index < 0)
        {
            LogWarning("GetHeroUrlById | id no encontrado='" + id + "'.");
            return VRCUrl.Empty;
        }
        if (questBuild)
        {
            VRCUrl quest = GetUrlSafe(heroQuestUrls, index);
            if (!VRCUrl.IsNullOrEmpty(quest)) return quest;
        }

        VRCUrl url = GetUrlSafe(heroPcUrls, index);
        Log("GetHeroUrlById | id='" + id + "' | questBuild=" + questBuild + " | url=" + (url == null ? "null" : url.Get()));
        return url;
    }

    public VRCUrl GetMainUrlByIndex(int index)
    {
        return GetUrlSafe(mainUrls, index);
    }

    public VRCUrl GetAltUrlByIndex(int index)
    {
        return GetUrlSafe(altUrls, index);
    }

    public VRCUrl GetPosterUrlByIndex(int index, bool questBuild)
    {
        if (questBuild)
        {
            VRCUrl quest = GetUrlSafe(posterQuestUrls, index);
            if (!VRCUrl.IsNullOrEmpty(quest)) return quest;
        }

        VRCUrl url = GetUrlSafe(posterPcUrls, index);
        Log("GetPosterUrlByIndex | index=" + index + " | questBuild=" + questBuild + " | url=" + (url == null ? "null" : url.Get()));
        return url;
    }

    public VRCUrl GetHeroUrlByIndex(int index, bool questBuild)
    {
        if (questBuild)
        {
            VRCUrl quest = GetUrlSafe(heroQuestUrls, index);
            if (!VRCUrl.IsNullOrEmpty(quest)) return quest;
        }

        VRCUrl url = GetUrlSafe(heroPcUrls, index);
        Log("GetHeroUrlByIndex | index=" + index + " | questBuild=" + questBuild + " | url=" + (url == null ? "null" : url.Get()));
        return url;
    }

    private VRCUrl GetUrlSafe(VRCUrl[] array, int index)
    {
        if (array == null) return VRCUrl.Empty;
        if (index < 0 || index >= array.Length) return VRCUrl.Empty;
        if (array[index] == null) return VRCUrl.Empty;
        return array[index];
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    [ContextMenu("1. Generar 500 IDs Secuenciales")]
    public void GenerateSequentialIds()
    {
        Undo.RecordObject(this, "Generate IDs");
        if (ids == null || ids.Length == 0) return;
        for (int i = 0; i < ids.Length; i++) ids[i] = "movie_" + (i + 1).ToString("D3");
        EditorUtility.SetDirty(this);
        Debug.Log("[CinemaUrlRegistry] IDs generados.");
    }

    [ContextMenu("2. Generar 500 Links (Render)")]
    public void AutoFillRenderUrls()
    {
        Undo.RecordObject(this, "Auto Fill Rules");
        if (ids == null || ids.Length == 0) return;
        string baseUrl = string.IsNullOrEmpty(serverBaseUrl) ? "https://micinema-api.onrender.com" : serverBaseUrl.TrimEnd('/');
        
        mainUrls = new VRCUrl[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            if (!string.IsNullOrEmpty(ids[i])) mainUrls[i] = new VRCUrl(baseUrl + "/" + ids[i]);
            else mainUrls[i] = VRCUrl.Empty;
        }
        EditorUtility.SetDirty(this);
        Debug.Log("[CinemaUrlRegistry] URLs de Render generadas correctamente.");
    }
#endif
}

#if !COMPILER_UDONSHARP && UNITY_EDITOR
[CustomEditor(typeof(CinemaUrlRegistry))]
public class CinemaUrlRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CinemaUrlRegistry script = (CinemaUrlRegistry)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🔧 HERRAMIENTAS DE AUTO-CONFIGURACIÓN", EditorStyles.boldLabel);
        
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // Verde claro
        if (GUILayout.Button("PASO 1: GENERAR IDs (movie_001, ...)", GUILayout.Height(35)))
        {
            script.GenerateSequentialIds();
        }

        GUI.backgroundColor = new Color(0.7f, 0.7f, 1f); // Azul claro
        if (GUILayout.Button("PASO 2: GENERAR LINKS DE VIDEO (500 URLs)", GUILayout.Height(35)))
        {
            script.AutoFillRenderUrls();
        }
        
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);
        
        DrawDefaultInspector();
    }
}
#endif
