using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;
using VRC.SDK3.Image;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CinemaPlaybackController : UdonSharpBehaviour
{
    [Header("Debug")]
    public bool enableDebugLogs = true;

    [Header("ProTV TVManager")]
    public UdonSharpBehaviour proTV;

    [Header("UI")]
    public TMP_Text nowPlayingLabel;
    public TMP_Text statusLabel;

    [Header("Synced State")]
    [UdonSynced] private string _syncedMovieId = "";
    [UdonSynced] private string _syncedTitle = "";
    [UdonSynced] private int _syncNonce;

    [Header("Security")]
    public VRCUrl knockUrl;

    private VRCImageDownloader _downloader;

    private void Log(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log("[CinemaPlaybackController] " + message);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogWarning("[CinemaPlaybackController] " + message);
    }

    private void LogError(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogError("[CinemaPlaybackController] " + message);
    }

    private void Start()
    {
        Log("Start | isMaster=" + Networking.IsMaster);
        RefreshNowPlayingLabel();
    }

    public bool CanLocalPlayerControl()
    {
        bool canControl = Networking.IsMaster;
        Log("CanLocalPlayerControl | result=" + canControl);
        return canControl;
    }

    public void PlayMedia(string movieId, string title, VRCUrl mainUrl, VRCUrl altUrl)
    {
        Log("PlayMedia | movieId='" + movieId + "' | title='" + title + "' | mainUrl=" + (mainUrl == null ? "null" : mainUrl.Get()) + " | altUrl=" + (altUrl == null ? "null" : altUrl.Get()));
        if (!Networking.IsMaster)
        {
            LogWarning("PlayMedia cancelado porque el jugador local no es master.");
            SetStatus("Solo el master de la instancia puede reproducir contenido.");
            return;
        }

        if (proTV == null)
        {
            LogError("PlayMedia cancelado: proTV no asignado.");
            SetStatus("Falta asignar el TVManager de ProTV.");
            return;
        }

        if (VRCUrl.IsNullOrEmpty(mainUrl) && VRCUrl.IsNullOrEmpty(altUrl))
        {
            LogError("PlayMedia cancelado: mainUrl y altUrl vacías.");
            SetStatus("La película seleccionada no tiene URL asignada en el registry.");
            return;
        }

        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        _syncedMovieId = movieId;
        _syncedTitle = title;
        _syncNonce++;
        Log("Solicitando ownership y serialización.");
        RequestSerialization();

        // === SISTEMA DE SEGURIDAD: IP KNOCKING ===
        if (!VRCUrl.IsNullOrEmpty(knockUrl))
        {
            Log("Tocando a la puerta: " + knockUrl.Get());
            if (_downloader == null) _downloader = new VRCImageDownloader();
            // Pasamos 'this' como receptor para que Udon esté feliz
            _downloader.DownloadImage(knockUrl, null, (IUdonEventReceiver)this, null);
        }

        proTV.SetProgramVariable("IN_MAINURL", mainUrl == null ? VRCUrl.Empty : mainUrl);
        proTV.SetProgramVariable("IN_ALTURL", altUrl == null ? VRCUrl.Empty : altUrl);
        proTV.SetProgramVariable("IN_TITLE", title == null ? "" : title);
        proTV.SendCustomEvent("_ChangeMedia");

        RefreshNowPlayingLabel();
        SetStatus("Reproduciendo: " + title);
        Log("PlayMedia enviado a ProTV correctamente.");
    }

    // Métodos obligatorios para que VRCImageDownloader no de error
    public override void OnImageLoadSuccess(IVRCImageDownload result) { Log("Toque a la puerta exitoso."); }
    public override void OnImageLoadError(IVRCImageDownload result) { Log("Toque a la puerta completado (con error, pero la IP ya se registró)."); }

    public string GetNowPlayingMovieId()
    {
        return _syncedMovieId;
    }

    public string GetNowPlayingTitle()
    {
        return _syncedTitle;
    }

    public override void OnDeserialization()
    {
        Log("OnDeserialization | syncedMovieId='" + _syncedMovieId + "' | syncedTitle='" + _syncedTitle + "'.");
        RefreshNowPlayingLabel();
    }

    private void RefreshNowPlayingLabel()
    {
        if (nowPlayingLabel == null) return;

        if (string.IsNullOrEmpty(_syncedTitle))
        {
            nowPlayingLabel.text = "Ahora reproduciendo: ---";
        }
        else
        {
            nowPlayingLabel.text = "Ahora reproduciendo: " + _syncedTitle;
        }
    }

    private void SetStatus(string value)
    {
        if (statusLabel != null) statusLabel.text = value;
        Log("Status | " + value);
    }
}
