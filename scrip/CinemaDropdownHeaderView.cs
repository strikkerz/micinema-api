using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CinemaDropdownHeaderView : UdonSharpBehaviour
{
    [Header("Configuration")]
    public string categoryName = "Genre";
    
    [Header("UI References")]
    public TMP_Text labelText;
    public GameObject parentMenu; 

    public void ToggleMenu()
    {
        if (parentMenu != null)
        {
            bool newState = !parentMenu.activeSelf;
            Debug.Log("[CinemaDropdownHeaderView] ToggleMenu | category='" + categoryName + "' | Setting parentMenu (" + parentMenu.name + ") active=" + newState);
            parentMenu.SetActive(newState);
        }
        else
        {
            Debug.LogWarning("[CinemaDropdownHeaderView] ToggleMenu falló: parentMenu no está asignado en '" + gameObject.name + "'.");
        }
    }

    public void SetLabel(string text)
    {
        if (labelText != null) labelText.text = text;
    }
}
