using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CinemaUrlRegistry : UdonSharpBehaviour
{
    [Header("Debug")]
    public bool enableDebugLogs = true;

    [Header("Security")]
    [Tooltip("Clave de seguridad que debe coincidir con la de tu servidor API.")]
    public string secretKey = "M1C1N3M4_S3CR3T_K3Y";

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

    private void Log(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log("[CinemaUrlRegistry] " + message);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLogs) return;
        Debug.LogWarning("[CinemaUrlRegistry] " + message);
    }

    public int FindIndexById(string id)
    {
        if (ids == null || string.IsNullOrEmpty(id)) return -1;

        int count = ids.Length;
        for (int i = 0; i < count; i++)
        {
            if (ids[i] == id) return i;
        }

        return -1;
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
    [ContextMenu("Auto-completar URLs (Localhost)")]
    public void AutoFillLocalhostUrls()
    {
        UnityEditor.Undo.RecordObject(this, "Auto Fill URLs");
        if (ids == null) return;
        
        mainUrls = new VRCUrl[ids.Length];
        
        for (int i = 0; i < ids.Length; i++)
        {
            if (!string.IsNullOrEmpty(ids[i]))
            {
                mainUrls[i] = new VRCUrl("http://localhost:3000/" + ids[i] + "?key=" + secretKey);
            }
            else
            {
                mainUrls[i] = VRCUrl.Empty;
            }
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
