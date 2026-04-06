using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;
using VRC.SDK3.Image;

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
        // Antes de que ProTV pida el video, nosotros avisamos al servidor
        // Esto autoriza nuestra IP durante 60 segundos.
        if (mainUrl != null)
        {
            string baseUrl = mainUrl.Get();
            if (baseUrl.Contains(".onrender.com"))
            {
                // Obtenemos la URL base (sin el ID) para construir la ruta /knock/ID
                int lastSlash = baseUrl.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    string knockUrl = baseUrl.Substring(0, lastSlash) + "/knock/" + movieId;
                    Log("Tocando a la puerta: " + knockUrl);
                    if (_downloader == null) _downloader = new VRCImageDownloader();
                    _downloader.DownloadImage(new VRCUrl(knockUrl), null, null, null);
                }
            }
        }

        proTV.SetProgramVariable("IN_MAINURL", mainUrl == null ? VRCUrl.Empty : mainUrl);
        proTV.SetProgramVariable("IN_ALTURL", altUrl == null ? VRCUrl.Empty : altUrl);
        proTV.SetProgramVariable("IN_TITLE", title == null ? "" : title);
        proTV.SendCustomEvent("_ChangeMedia");

        RefreshNowPlayingLabel();
        SetStatus("Reproduciendo: " + title);
        Log("PlayMedia enviado a ProTV correctamente.");
    }

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
