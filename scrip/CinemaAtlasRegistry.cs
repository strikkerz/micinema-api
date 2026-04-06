using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Image;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CinemaAtlasRegistry : UdonSharpBehaviour
{
    [Header("Debug")]
    public bool enableDebugLogs = true;

    [Header("Atlas Keys")]
    public string[] atlasKeys;

    [Header("Atlas URLs")]
    public VRCUrl[] atlasPcUrls;
    public VRCUrl[] atlasQuestUrls;

    [Header("Sequential Downloader Settings")]
    public bool generateMipMaps = true;
    public FilterMode filterMode = FilterMode.Bilinear;
    [Tooltip("Tiempo en segundos entre la descarga de un atlas y el siguiente.")]
    public float delaySeconds = 5.0f;

    private Texture[] _cachedTextures;
    private bool[] _isLoading;
    private bool[] _isReady;

    private VRCImageDownloader _downloader;
    private TextureInfo _textureInfo;

    // Background Sequential State
    private int _currentIndex;
    private int _totalAtlases;

    [Header("Callbacks")]
    public UdonSharpBehaviour completionTarget;
    public string completionEvent = "OnAtlasQueueComplete";

    private void Log(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log("[CinemaAtlasRegistry] " + message);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning("[CinemaAtlasRegistry] " + message);
    }

    private void Start()
    {
        int count = atlasKeys != null ? atlasKeys.Length : 0;
        _cachedTextures = new Texture[count];
        _isLoading = new bool[count];
        _isReady = new bool[count];

        _downloader = new VRCImageDownloader();
        _textureInfo = new TextureInfo();
        _textureInfo.GenerateMipMaps = generateMipMaps;
        _textureInfo.FilterMode = filterMode;
    }

    public int FindIndexByKey(string atlasKey)
    {
        if (atlasKeys == null || string.IsNullOrEmpty(atlasKey)) return -1;
        for (int i = 0; i < atlasKeys.Length; i++)
        {
            if (atlasKeys[i] == atlasKey) return i;
        }
        return -1;
    }

    public Texture GetCachedTexture(string atlasKey)
    {
        int idx = FindIndexByKey(atlasKey);
        if (idx < 0) return null;
        return _cachedTextures[idx];
    }

    public void StartSequentialDownload()
    {
        Log("StartSequentialDownload llamado por el Manager.");

        if (atlasKeys == null || atlasKeys.Length == 0)
        {
            Log("No hay atlas en la cola. Notificando finalización inmediata...");
            _NotifyComplete();
            return;
        }

        Log("Iniciando cola de descargas | total=" + atlasKeys.Length);
        _currentIndex = 0;
        _totalAtlases = atlasKeys.Length;

        _ProcessNextInQueue();
    }

    public void _ProcessNextInQueue()
    {
        if (_currentIndex >= _totalAtlases)
        {
            Log("Descarga secuencial COMPLETA.");
            _NotifyComplete();
            return;
        }

        int idx = _currentIndex;
        string key = atlasKeys[idx];

        if (_isReady[idx] || _isLoading[idx])
        {
            _currentIndex++;
            _ProcessNextInQueue();
            return;
        }

        VRCUrl url = GetAtlasUrlByKey(key, IsQuestBuild());
        if (VRCUrl.IsNullOrEmpty(url))
        {
            _currentIndex++;
            _ProcessNextInQueue();
            return;
        }

        _isLoading[idx] = true;
        Log("Descargando secuencialmente (A-Z): " + key + " | URL=" + url.Get());
        _downloader.DownloadImage(url, null, (IUdonEventReceiver)this, _textureInfo);
    }

    private void _NotifyComplete()
    {
        if (completionTarget != null && !string.IsNullOrEmpty(completionEvent))
        {
            Log("Enviando señal de completado a: " + completionTarget.gameObject.name + " (" + completionEvent + ")");
            completionTarget.SendCustomEvent(completionEvent);
        }
        else
        {
            LogWarning("No se pudo enviar la señal de completado: 'completionTarget' o 'completionEvent' no están configurados.");
        }
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        string url = result.Url.Get();
        int idx = -1;
        
        bool isQuest = IsQuestBuild();
        for(int i=0; i<atlasKeys.Length; i++)
        {
            VRCUrl u = isQuest ? atlasQuestUrls[i] : atlasPcUrls[i];
            if (u != null && u.Get() == url) { idx = i; break; }
        }

        if (idx >= 0)
        {
            _cachedTextures[idx] = result.Result;
            _isReady[idx] = true;
            _isLoading[idx] = false;
            Log("Atlas CARGADO exitosamente: " + atlasKeys[idx]);
        }

        // Llamar a la siguiente descarga inmediatamente
        _currentIndex++;
        _ProcessNextInQueue();
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        Log("Error cargando atlas: " + (result.Url != null ? result.Url.Get() : "null"));
        _currentIndex++;
        _ProcessNextInQueue();
    }

    public VRCUrl GetAtlasUrlByKey(string atlasKey, bool questBuild)
    {
        int index = FindIndexByKey(atlasKey);
        if (index < 0) return VRCUrl.Empty;

        if (questBuild)
        {
            VRCUrl quest = GetUrlSafe(atlasQuestUrls, index);
            if (!VRCUrl.IsNullOrEmpty(quest)) return quest;
        }

        return GetUrlSafe(atlasPcUrls, index);
    }

    private VRCUrl GetUrlSafe(VRCUrl[] array, int index)
    {
        if (array == null || index < 0 || index >= array.Length) return VRCUrl.Empty;
        return array[index];
    }

    private bool IsQuestBuild()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }
}
