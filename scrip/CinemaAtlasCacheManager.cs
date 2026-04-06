using UdonSharp;
using UnityEngine;
using VRC.SDK3.Image;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CinemaAtlasCacheManager : UdonSharpBehaviour
{
    [Header("Debug")]
    public bool enableDebugLogs = true;

    [Header("Atlas Source")]
    public CinemaAtlasRegistry atlasRegistry;
    public int maxCachedAtlases = 5;

    [Header("Texture Settings")]
    public bool generateMipMaps = true;
    public FilterMode filterMode = FilterMode.Bilinear;

    private VRCImageDownloader _downloader;
    private TextureInfo _textureInfo;

    private string[] _slotKeys = new string[0];
    private string[] _slotUrls = new string[0];
    private Texture[] _slotTextures = new Texture[0];
    private IVRCImageDownload[] _slotDownloads = new IVRCImageDownload[0];
    private bool[] _slotLoading = new bool[0];
    private int[] _slotLastUsed = new int[0];

    private CinemaRemoteImage[] _views = new CinemaRemoteImage[256];
    private string[] _viewKeys = new string[256];

    private int _tick;

    private void Log(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log("[CinemaAtlasCacheManager] " + message);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogWarning("[CinemaAtlasCacheManager] " + message);
    }

    private void LogError(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogError("[CinemaAtlasCacheManager] " + message);
    }

    private void Start()
    {
        if (maxCachedAtlases <= 0) maxCachedAtlases = 5;
        Log("Start | maxCachedAtlases=" + maxCachedAtlases + " | generateMipMaps=" + generateMipMaps + " | filterMode=" + filterMode);

        _slotKeys = new string[maxCachedAtlases];
        _slotUrls = new string[maxCachedAtlases];
        _slotTextures = new Texture[maxCachedAtlases];
        _slotDownloads = new IVRCImageDownload[maxCachedAtlases];
        _slotLoading = new bool[maxCachedAtlases];
        _slotLastUsed = new int[maxCachedAtlases];

        _downloader = new VRCImageDownloader();
        _textureInfo = new TextureInfo();
        _textureInfo.GenerateMipMaps = generateMipMaps;
        _textureInfo.FilterMode = filterMode;
        _textureInfo.WrapModeU = TextureWrapMode.Clamp;
        _textureInfo.WrapModeV = TextureWrapMode.Clamp;
        _textureInfo.WrapModeW = TextureWrapMode.Clamp;
    }

    public void RequestAtlas(CinemaRemoteImage view, string atlasKey)
    {
        Log("RequestAtlas | view=" + (view == null ? "null" : view.gameObject.name) + " | atlasKey=" + atlasKey);
        if (view == null) return;
        if (string.IsNullOrEmpty(atlasKey))
        {
            ReleaseView(view);
            view.ApplyAtlasTexture("", null);
            return;
        }

        RegisterView(view, atlasKey);
        _tick++;

        // PRIORIDAD: Revisar si el Registry ya tiene la textura (pre-carga masiva)
        if (atlasRegistry != null)
        {
            Texture preloaded = atlasRegistry.GetCachedTexture(atlasKey);
            if (preloaded != null)
            {
                Log("Atlas pre-cargado en Registry encontrado | key=" + atlasKey);
                view.ApplyAtlasTexture(atlasKey, preloaded);
                return;
            }
        }

        int existingSlot = FindSlotByKey(atlasKey);
        if (existingSlot >= 0)
        {
            _slotLastUsed[existingSlot] = _tick;
            if (_slotTextures[existingSlot] != null)
            {
                Log("Atlas ya en caché de slots | slot=" + existingSlot + " | key=" + atlasKey);
                view.ApplyAtlasTexture(atlasKey, _slotTextures[existingSlot]);
                return;
            }

            if (_slotLoading[existingSlot])
            {
                Log("Atlas en descarga en slot | slot=" + existingSlot + " | key=" + atlasKey);
                return;
            }
        }

        if (atlasRegistry == null)
        {
            LogError("atlasRegistry no asignado para atlasKey='" + atlasKey + "'.");
            view.ApplyAtlasTexture(atlasKey, null);
            return;
        }

        VRCUrl atlasUrl = atlasRegistry.GetAtlasUrlByKey(atlasKey, IsQuestBuild());
        if (VRCUrl.IsNullOrEmpty(atlasUrl))
        {
            LogWarning("No se encontró URL para atlasKey='" + atlasKey + "'.");
            view.ApplyAtlasTexture(atlasKey, null);
            return;
        }

        int slot = existingSlot;
        if (slot < 0)
        {
            slot = ReserveSlot(atlasKey);
        }

        if (slot < 0)
        {
            LogError("No se pudo reservar slot para atlasKey='" + atlasKey + "'.");
            view.ApplyAtlasTexture(atlasKey, null);
            return;
        }

        DisposeSlotDownload(slot);

        _slotKeys[slot] = atlasKey;
        _slotUrls[slot] = atlasUrl.Get();
        _slotTextures[slot] = null;
        _slotLoading[slot] = true;
        _slotLastUsed[slot] = _tick;
        Log("Iniciando descarga atlas | slot=" + slot + " | key=" + atlasKey + " | url=" + _slotUrls[slot]);
        _slotDownloads[slot] = _downloader.DownloadImage(atlasUrl, null, (IUdonEventReceiver)this, _textureInfo);
    }

    public void ReleaseView(CinemaRemoteImage view)
    {
        if (view == null) return;
        Log("ReleaseView | view=" + view.gameObject.name);

        for (int i = 0; i < _views.Length; i++)
        {
            if (_views[i] == view)
            {
                _views[i] = null;
                _viewKeys[i] = "";
                return;
            }
        }
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        if (result == null || result.Url == null) return;

        string url = result.Url.Get();
        int slot = FindSlotByUrl(url);
        if (slot < 0) return;

        _slotDownloads[slot] = null;
        _slotLoading[slot] = false;
        _slotTextures[slot] = result.Result;
        Log("OnImageLoadSuccess | slot=" + slot + " | key=" + _slotKeys[slot] + " | url=" + url + " | hasTexture=" + (result.Result != null));
        NotifyViews(_slotKeys[slot], _slotTextures[slot]);
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        if (result == null || result.Url == null) return;

        int slot = FindSlotByUrl(result.Url.Get());
        if (slot < 0) return;

        _slotDownloads[slot] = null;
        string key = _slotKeys[slot];
        LogError("OnImageLoadError | slot=" + slot + " | key=" + key + " | url=" + result.Url.Get());
        ClearSlot(slot);
        NotifyViews(key, null);
    }

    private void RegisterView(CinemaRemoteImage view, string atlasKey)
    {
        int freeIndex = -1;

        for (int i = 0; i < _views.Length; i++)
        {
            if (_views[i] == view)
            {
                _viewKeys[i] = atlasKey;
                return;
            }

            if (freeIndex < 0 && _views[i] == null)
            {
                freeIndex = i;
            }
        }

        if (freeIndex >= 0)
        {
            _views[freeIndex] = view;
            _viewKeys[freeIndex] = atlasKey;
            Log("RegisterView | index=" + freeIndex + " | view=" + view.gameObject.name + " | atlasKey=" + atlasKey);
        }
        else
        {
            LogWarning("No hay slot libre para registrar view='" + view.gameObject.name + "' con atlasKey='" + atlasKey + "'.");
        }
    }

    private void NotifyViews(string atlasKey, Texture texture)
    {
        if (string.IsNullOrEmpty(atlasKey)) return;

        for (int i = 0; i < _views.Length; i++)
        {
            if (_views[i] == null) continue;
            if (_viewKeys[i] != atlasKey) continue;
            _views[i].ApplyAtlasTexture(atlasKey, texture);
        }
    }

    private int ReserveSlot(string nextKey)
    {
        int freeSlot = -1;
        for (int i = 0; i < _slotKeys.Length; i++)
        {
            if (string.IsNullOrEmpty(_slotKeys[i]))
            {
                freeSlot = i;
                break;
            }
        }

        if (freeSlot >= 0)
        {
            Log("ReserveSlot | usando slot libre=" + freeSlot + " para key='" + nextKey + "'.");
            return freeSlot;
        }

        int bestSlot = -1;
        int bestTick = int.MaxValue;

        for (int i = 0; i < _slotKeys.Length; i++)
        {
            if (_slotKeys[i] == nextKey) return i;
            if (CountViewsUsingKey(_slotKeys[i]) > 0) continue;
            if (_slotLastUsed[i] < bestTick)
            {
                bestTick = _slotLastUsed[i];
                bestSlot = i;
            }
        }

        if (bestSlot < 0)
        {
            for (int i = 0; i < _slotKeys.Length; i++)
            {
                if (_slotLastUsed[i] < bestTick)
                {
                    bestTick = _slotLastUsed[i];
                    bestSlot = i;
                }
            }
        }

        if (bestSlot >= 0)
        {
            string oldKey = _slotKeys[bestSlot];
            Log("ReserveSlot | reciclando slot=" + bestSlot + " | oldKey='" + oldKey + "' | nextKey='" + nextKey + "'.");
            ClearSlot(bestSlot);
        }

        return bestSlot;
    }

    private int FindSlotByKey(string atlasKey)
    {
        for (int i = 0; i < _slotKeys.Length; i++)
        {
            if (_slotKeys[i] == atlasKey) return i;
        }

        return -1;
    }

    private int FindSlotByUrl(string url)
    {
        for (int i = 0; i < _slotUrls.Length; i++)
        {
            if (_slotUrls[i] == url) return i;
        }

        return -1;
    }

    private int CountViewsUsingKey(string atlasKey)
    {
        if (string.IsNullOrEmpty(atlasKey)) return 0;

        int count = 0;
        for (int i = 0; i < _viewKeys.Length; i++)
        {
            if (_views[i] == null) continue;
            if (_viewKeys[i] != atlasKey) continue;
            count++;
        }

        return count;
    }

    private void ClearSlot(int slot)
    {
        if (slot < 0 || slot >= _slotKeys.Length) return;

        Log("ClearSlot | slot=" + slot + " | key='" + _slotKeys[slot] + "'.");
        DisposeSlotDownload(slot);
        _slotKeys[slot] = "";
        _slotUrls[slot] = "";
        _slotTextures[slot] = null;
        _slotLoading[slot] = false;
        _slotLastUsed[slot] = 0;
    }

    private void DisposeSlotDownload(int slot)
    {
        if (slot < 0 || slot >= _slotDownloads.Length) return;

        if (_slotDownloads[slot] != null)
        {
            Log("DisposeSlotDownload | slot=" + slot + " | key='" + _slotKeys[slot] + "'.");
            _slotDownloads[slot].Dispose();
            _slotDownloads[slot] = null;
        }
    }

    private bool IsQuestBuild()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }

    private void OnDestroy()
    {
        Log("OnDestroy | liberando descargas y downloader.");
        for (int i = 0; i < _slotDownloads.Length; i++)
        {
            DisposeSlotDownload(i);
        }

        if (_downloader != null)
        {
            _downloader.Dispose();
            _downloader = null;
        }
    }
}
