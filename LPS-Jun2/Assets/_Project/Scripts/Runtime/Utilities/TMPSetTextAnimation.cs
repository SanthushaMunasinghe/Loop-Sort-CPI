using System;
using TMPro;
using UnityEngine;

public sealed class TMPSetTextAnimation : MonoBehaviour
{
    [SerializeField] private TMP_Text Text;

    private string _originalText;

    private void Awake()
    {
        _originalText =  Text.text;
    }

    public void SetTextInt(AnimationEvent animationEvent)
    {
        var intParameter = animationEvent.intParameter;
        var format = string.Format(_originalText, intParameter.ToString());
        Text.SetText(format);
    }
}