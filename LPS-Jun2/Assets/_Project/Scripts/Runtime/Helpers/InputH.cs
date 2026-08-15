using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public static class InputH
{
    private static bool GetAvailable()
    {
        if (IsTypingInInputField()) return false;
        return true;
    }

    private static bool IsTypingInInputField()
    {
        var selectedObject = EventSystem.current?.currentSelectedGameObject;
        if (selectedObject == null) return false;

        var inputField = selectedObject.GetComponentInParent<TMP_InputField>();
        return inputField != null && inputField.isFocused;
    }

    public static bool GetKey(KeyCode keyCode) => GetAvailable() && Input.GetKey(keyCode);

    public static bool GetKeyDown(KeyCode keyCode) => GetAvailable() && Input.GetKeyDown(keyCode);

    public static bool GetKeyUp(KeyCode keyCode) => GetAvailable() && Input.GetKeyUp(keyCode);
}