using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Image;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CinemaRemoteImage : UdonSharpBehaviour
{
    [Header("Debug")]
    public bool enableDebugLogs = true;

    [Header("Targets")]
    public RawImage rawImageTarget;
    public Renderer rendererTarget;
    public string materialProperty = "_MainTex";

    [Header("Fallback")]
    public Texture defaultTexture;
    public Color loadingColor = Color.black;
    public Color loadedColor = Color.white;

    [Header("Direct URL Texture Settings")]
    public bool generateMipMaps = true;
    public FilterMode filterMode = FilterMode.Bilinear;

    [Header("Atlas Mode")]
    public CinemaAtlasCacheManager atlasCache;
    public int atlasColumns = 16;
    public int atlasRows = 8;

    [Header("Callbacks (Optional)")]
    public UdonSharpBehaviour callbackTarget;
    public string callbackEvent = "OnImageLoaded";

    private VRCImageDownloader _downloader;
    private IVRCImageDownload _activeDownload;
    private string _currentUrl = "";
    private VRCUrl _currentVrcUrl = VRCUrl.Empty;
    private TextureInfo _textureInfo;
    private Material _runtimeMaterial;

    private bool _atlasMode;
    private string _currentAtlasKey = "";
    private int _currentAtlasCell = -1;
    private bool _needsReload;

    private void Log(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log("[CinemaRemoteImage] " + message);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogWarning("[CinemaRemoteImage] " + message);
    }

    private void LogError(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogError("[CinemaRemoteImage] " + message);
    }

    private void Start()
    {
        Log("Start | object=" + gameObject.name + " | atlasColumns=" + atlasColumns + " | atlasRows=" + atlasRows + " | materialProperty='" + materialProperty + "'.");
        EnsureInitialized();
        ResetUv();
        ApplyTexture(defaultTexture);
        
        // Al iniciar, si no hay textura por defecto que sea una imagen real, 
        // forzamos el color de carga (negro) para que no se vea blanco vacío.
        if (defaultTexture == null) 
        {
            ApplyColor(loadingColor);
        }
    }

    private void OnEnable()
    {
        if (!_needsReload) return;

        Log("OnEnable | object=" + gameObject.name + " | atlasMode=" + _atlasMode + " | currentUrl='" + _currentUrl + "' | atlasKey='" + _currentAtlasKey + "'.");
        EnsureInitialized();

        if (_atlasMode)
        {
            if (!string.IsNullOrEmpty(_currentAtlasKey) && _currentAtlasCell >= 0)
            {
                ApplyAtlasUv(_currentAtlasCell);
                ApplyTexture(defaultTexture);
                if (atlasCache != null)
                {
                    atlasCache.RequestAtlas(this, _currentAtlasKey);
                }
            }

            return;
        }

        if (!VRCUrl.IsNullOrEmpty(_currentVrcUrl))
        {
            StartDirectDownload(_currentVrcUrl, true);
        }
    }

    public void SetUrl(VRCUrl url)
    {
        Log("SetUrl | object=" + gameObject.name + " | url=" + (url == null ? "null" : url.Get()));
        _atlasMode = false;
        _needsReload = false;
        if (atlasCache != null) atlasCache.ReleaseView(this);
        _currentAtlasKey = "";
        _currentAtlasCell = -1;
        ResetUv();

        if (VRCUrl.IsNullOrEmpty(url))
        {
            LogWarning("SetUrl recibió URL vacía. Se limpiará la imagen.");
            ClearImage();
            return;
        }

        string nextUrl = url.Get();
        if (_currentUrl == nextUrl && !_needsReload)
        {
            Log("SetUrl omitido porque la URL ya estaba activa y no requiere recarga.");
            return;
        }

        _currentUrl = nextUrl;
        _currentVrcUrl = url;
        StartDirectDownload(url, false);
    }

    public void SetAtlasCell(string atlasKey, int cellIndex)
    {
        Log("SetAtlasCell | object=" + gameObject.name + " | atlasKey='" + atlasKey + "' | cellIndex=" + cellIndex);
        if (string.IsNullOrEmpty(atlasKey) || cellIndex < 0)
        {
            ClearImage();
            return;
        }

        _atlasMode = true;
        _needsReload = false;
        _currentUrl = "";
        _currentVrcUrl = VRCUrl.Empty;
        DisposeActiveDownload();

        _currentAtlasKey = atlasKey;
        _currentAtlasCell = cellIndex;
        ApplyAtlasUv(cellIndex);
        ApplyTexture(defaultTexture);

        if (atlasCache != null)
        {
            atlasCache.RequestAtlas(this, atlasKey);
        }
        else
        {
            LogWarning("SetAtlasCell sin atlasCache asignado.");
        }
    }

    public void ApplyAtlasTexture(string atlasKey, Texture texture)
    {
        if (!_atlasMode) return;
        if (_currentAtlasKey != atlasKey) return;

        Log("ApplyAtlasTexture | object=" + gameObject.name + " | atlasKey='" + atlasKey + "' | hasTexture=" + (texture != null));

        ApplyAtlasUv(_currentAtlasCell);
        ApplyTexture(texture != null ? texture : defaultTexture);
        _needsReload = false;

        if (callbackTarget != null) callbackTarget.SendCustomEvent(callbackEvent);
    }

    public void ClearImage()
    {
        Log("ClearImage | object=" + gameObject.name);
        _currentUrl = "";
        _currentVrcUrl = VRCUrl.Empty;
        DisposeActiveDownload();

        if (atlasCache != null)
        {
            atlasCache.ReleaseView(this);
        }

        _atlasMode = false;
        _currentAtlasKey = "";
        _currentAtlasCell = -1;
        _needsReload = false;
        ResetUv();
        ApplyTexture(defaultTexture);
        
        if (defaultTexture == null) 
        {
            ApplyColor(loadingColor);
        }
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        if (_atlasMode) return;
        if (result == null) return;
        if (result.Url == null) return;

        string url = result.Url.Get();
        if (url != _currentUrl)
        {
            LogWarning("OnImageLoadSuccess ignorado por URL desfasada | resultUrl=" + url + " | currentUrl=" + _currentUrl);
            return;
        }

        _activeDownload = null;
        _needsReload = false;
        Log("OnImageLoadSuccess | url=" + url + " | hasTexture=" + (result.Result != null));
        ResetUv();
        ApplyTexture(result.Result != null ? result.Result : defaultTexture);
        
        if (callbackTarget != null) callbackTarget.SendCustomEvent(callbackEvent);
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        if (_atlasMode) return;
        _activeDownload = null;
        _needsReload = false;
        LogError("OnImageLoadError | url=" + (result == null || result.Url == null ? "null" : result.Url.Get()));
        ResetUv();
        ApplyTexture(defaultTexture);

        if (callbackTarget != null) callbackTarget.SendCustomEvent(callbackEvent);
    }

    private void ApplyAtlasUv(int cellIndex)
    {
        if (atlasColumns <= 0) atlasColumns = 16;
        if (atlasRows <= 0) atlasRows = 8;

        float width = 1f / atlasColumns;
        float height = 1f / atlasRows;
        int col = Mathf.Max(0, cellIndex) % atlasColumns;
        int row = Mathf.Max(0, cellIndex) / atlasColumns;
        float x = col * width;
        float y = 1f - ((row + 1) * height);

        if (rawImageTarget != null)
        {
            rawImageTarget.uvRect = new Rect(x, y, width, height);
        }

        Material mat = GetRuntimeMaterial();
        if (mat != null)
        {
            mat.SetTextureScale(materialProperty, new Vector2(width, height));
            mat.SetTextureOffset(materialProperty, new Vector2(x, y));
        }
    }

    private void ResetUv()
    {
        if (rawImageTarget != null)
        {
            rawImageTarget.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        Material mat = GetRuntimeMaterial();
        if (mat != null)
        {
            mat.SetTextureScale(materialProperty, Vector2.one);
            mat.SetTextureOffset(materialProperty, Vector2.zero);
        }
    }

    private void ApplyTexture(Texture texture)
    {
        if (rawImageTarget != null)
        {
            rawImageTarget.texture = texture;
            rawImageTarget.color = texture != null ? loadedColor : loadingColor;
        }

        Material mat = GetRuntimeMaterial();
        if (mat != null)
        {
            mat.SetTexture(materialProperty, texture);
            // El tinte de color en materiales 3D depende del shader, lo omitimos por simplicidad en UI
        }
    }

    private void ApplyColor(Color color)
    {
        if (rawImageTarget != null)
        {
            rawImageTarget.color = color;
        }
    }

    private void StartDirectDownload(VRCUrl url, bool forceReload)
    {
        if (VRCUrl.IsNullOrEmpty(url))
        {
            ClearImage();
            return;
        }

        EnsureInitialized();

        if (!forceReload && _activeDownload != null && _currentUrl == url.Get())
        {
            Log("StartDirectDownload omitido porque ya existe una descarga activa para la misma URL.");
            return;
        }

        DisposeActiveDownload();
        _needsReload = false;
        ResetUv();
        ApplyTexture(defaultTexture);

        Log("Iniciando descarga directa | url=" + url.Get() + " | forceReload=" + forceReload);
        _activeDownload = _downloader.DownloadImage(url, null, (IUdonEventReceiver)this, _textureInfo);
    }

    private void EnsureInitialized()
    {
        if (_downloader == null)
        {
            _downloader = new VRCImageDownloader();
        }

        if (_textureInfo == null)
        {
            _textureInfo = new TextureInfo();
        }

        _textureInfo.GenerateMipMaps = generateMipMaps;
        _textureInfo.FilterMode = filterMode;
        _textureInfo.WrapModeU = TextureWrapMode.Clamp;
        _textureInfo.WrapModeV = TextureWrapMode.Clamp;
        _textureInfo.WrapModeW = TextureWrapMode.Clamp;
        _textureInfo.MaterialProperty = materialProperty;

        if (_runtimeMaterial == null && rendererTarget != null)
        {
            _runtimeMaterial = rendererTarget.material;
        }
    }

    private Material GetRuntimeMaterial()
    {
        if (_runtimeMaterial == null && rendererTarget != null)
        {
            _runtimeMaterial = rendererTarget.material;
        }

        return _runtimeMaterial;
    }

    private void DisposeActiveDownload()
    {
        if (_activeDownload != null)
        {
            Log("DisposeActiveDownload | url=" + _currentUrl);
            _activeDownload.Dispose();
            _activeDownload = null;
        }
    }

    private void OnDisable()
    {
        Log("OnDisable | object=" + gameObject.name);
        _needsReload = !VRCUrl.IsNullOrEmpty(_currentVrcUrl) || (_atlasMode && !string.IsNullOrEmpty(_currentAtlasKey));
        DisposeActiveDownload();
        if (atlasCache != null)
        {
            atlasCache.ReleaseView(this);
        }
        ApplyTexture(defaultTexture);
    }

    private void OnDestroy()
    {
        Log("OnDestroy | object=" + gameObject.name);
        DisposeActiveDownload();

        if (atlasCache != null)
        {
            atlasCache.ReleaseView(this);
        }

        if (_downloader != null)
        {
            _downloader.Dispose();
            _downloader = null;
        }

        _runtimeMaterial = null;
    }
}
