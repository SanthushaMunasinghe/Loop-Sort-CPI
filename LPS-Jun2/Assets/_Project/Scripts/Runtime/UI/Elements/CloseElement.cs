using StatefulUI.Runtime.Core;
using StatefulUISupport.Scripts.Components;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CloseElement : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        var view = GetComponentInParent<StatefulComponent>();
        view.GetButton(ButtonRole.Close).onClick.Invoke();
    }
}