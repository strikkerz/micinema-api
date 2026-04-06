using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CinemaCardView : UdonSharpBehaviour
{
    [Header("Debug")]
    public bool enableDebugLogs = true;

    public TMP_Text titleText;
    public TMP_Text metaText;
    public CinemaRemoteImage posterImage;
    public GameObject selectedMarker;
    public GameObject root;
    public GameObject loadingIcon;
    public float rotationSpeed = 200f;

    [HideInInspector] public CinemaCatalogManager manager;

    private int _catalogIndex = -1;
    private bool _isLoading;

    private void Update()
    {
        if (_isLoading && loadingIcon != null)
        {
            loadingIcon.transform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
        }
    }

    private void Log(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log("[CinemaCardView] " + message);
    }

    public void Configure(
        CinemaCatalogManager newManager,
        int catalogIndex,
        string titleValue,
        string metaValue,
        string posterAtlasKey,
        int posterAtlasCell,
        VRCUrl posterUrl,
        bool selected)
    {
        manager = newManager;
        _catalogIndex = catalogIndex;

        if (root != null) root.SetActive(true);
        if (titleText != null) titleText.text = titleValue;
        if (metaText != null) metaText.text = metaValue;
        if (selectedMarker != null) selectedMarker.SetActive(selected);
        
        if (loadingIcon != null && posterImage != null)
        {
            loadingIcon.SetActive(true);
            _isLoading = true;
        }
        else if (loadingIcon != null)
        {
            loadingIcon.SetActive(false);
            _isLoading = false;
        }

        Log("Configure | card=" + gameObject.name + " | catalogIndex=" + catalogIndex + " | title='" + titleValue + "' | atlasKey='" + posterAtlasKey + "' | atlasCell=" + posterAtlasCell + " | selected=" + selected);

        if (posterImage != null)
        {
            posterImage.callbackTarget = (UdonSharpBehaviour)this;
            posterImage.callbackEvent = "OnImageLoaded";

            if (!string.IsNullOrEmpty(posterAtlasKey) && posterAtlasCell >= 0)
            {
                posterImage.SetAtlasCell(posterAtlasKey, posterAtlasCell);
            }
            else
            {
                posterImage.SetUrl(posterUrl);
            }
        }
    }

    public void OnImageLoaded()
    {
        Log("OnImageLoaded | card=" + gameObject.name);
        _isLoading = false;
        if (loadingIcon != null) loadingIcon.SetActive(false);
    }

    public void Clear()
    {
        Log("Clear | card=" + gameObject.name);
        _catalogIndex = -1;

        if (root != null) root.SetActive(false);
        if (selectedMarker != null) selectedMarker.SetActive(false);
        if (loadingIcon != null) loadingIcon.SetActive(false);
        _isLoading = false;
        if (posterImage != null) posterImage.ClearImage();
    }

    public override void Interact()
    {
        Press();
    }

    public void Press()
    {
        Log("Press | card=" + gameObject.name + " | catalogIndex=" + _catalogIndex);
        if (manager == null) return;
        if (_catalogIndex < 0) return;
        manager.OnCardPressed(_catalogIndex);
    }
}
