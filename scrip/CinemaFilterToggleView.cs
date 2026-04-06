using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CinemaFilterToggleView : UdonSharpBehaviour
{
    [Header("Configuration")]
    [Tooltip("0: Genre, 1: Year, 2: Language, 3: Type")]
    public int filterType = 0; 
    [Tooltip("El valor exacto de este filtro en el JSON (ej. 'Accion', '2023', 'movie')")]
    public string filterValue = "";
    
    [Header("UI References")]
    public GameObject root;
    public Toggle toggleComponent;
    public TMP_Text labelText;
    public GameObject selectedMarker;

    [Header("Backend")]
    public CinemaCatalogManager manager;
    [Tooltip("Opcional: El objeto del menú (Template) para cerrarlo cuando se seleccione algo.")]
    public GameObject parentMenu; 
    private bool _isConfiguring = false;

    public void ToggleMenu()
    {
        if (parentMenu != null)
        {
            bool newState = !parentMenu.activeSelf;
            Debug.Log("[CinemaFilterToggleView] ToggleMenu | filterValue='" + filterValue + "' | Setting parentMenu active=" + newState);
            parentMenu.SetActive(newState);
        }
        else
        {
            Debug.LogWarning("[CinemaFilterToggleView] ToggleMenu falló: parentMenu no está asignado.");
        }
    }

    public void Configure(CinemaCatalogManager newManager, string value, bool isOn, bool interactable, bool visible)
    {
        // Si el manager intenta configurar un header por error, lo ignoramos de forma segura.
        if (filterType == -1) return; 

        manager = newManager;
        filterValue = value;

        _isConfiguring = true;

        if (root != null) root.SetActive(visible);

        if (labelText != null)
        {
            labelText.text = string.IsNullOrEmpty(value) ? "All" : value;
        }

        if (toggleComponent != null)
        {
            toggleComponent.interactable = interactable;
            toggleComponent.isOn = isOn;
        }

        // DEFENSA: Evitar que el chip se apague solo si selectedMarker == root (Boton Suicida)
        if (selectedMarker != null && selectedMarker != root)
        {
            selectedMarker.SetActive(isOn);
        }

        Debug.Log(
            "[CinemaFilterToggleView] Configure | go='" + gameObject.name +
            "' | value='" + value +
            "' | isOn=" + isOn +
            " | visible=" + visible +
            " | root=" + (root != null ? root.name : "null") +
            " | selectedMarker=" + (selectedMarker != null ? selectedMarker.name : "null") +
            " | sameRef=" + (selectedMarker == root) +
            " | parentMenu=" + (parentMenu != null ? parentMenu.name : "null")
        );

        // Retrasamos el final de la configuración para ignorar eventos de UI
        SendCustomEventDelayedFrames("_EndConfiguring", 1);
    }

    public void _EndConfiguring()
    {
        _isConfiguring = false;
    }

    // Alias de compatibilidad para prefabs que tengan mal escrito el nombre del evento
    public void Ontogglechanged() => OnToggleValueChanged();

    public void OnToggleValueChanged()
    {
        if (_isConfiguring) 
        {
            Debug.Log("[CinemaFilterToggleView] Ignorando cambio: El Manager está REFRESCANDO la UI (value='" + filterValue + "')");
            return;
        }
        if (manager == null) return;
        if (filterType == -1) 
        {
            Debug.Log("[CinemaFilterToggleView] Click detectado en CABECERA: No se aplicará filtro (filterType=-1)");
            return;
        }
        if (string.IsNullOrEmpty(filterValue)) return;

        bool isOn = false;
        if (toggleComponent != null) isOn = toggleComponent.isOn;

        Debug.Log("[CinemaFilterToggleView] CLICK DE USUARIO | value='" + filterValue + "' | Estado Nuevo=" + (isOn ? "ENCENDIDO" : "APAGADO") + " | filterType=" + filterType);
        manager.OnFilterToggled(filterType, filterValue, isOn);
        
        // Cierra el menú si se ha referenciado
        if (parentMenu != null && isOn) 
        {
            Debug.Log("[CinemaFilterToggleView] Selección realizada. Cerrando menú '" + parentMenu.name + "'.");
            parentMenu.SetActive(false);
        }
    }
}
