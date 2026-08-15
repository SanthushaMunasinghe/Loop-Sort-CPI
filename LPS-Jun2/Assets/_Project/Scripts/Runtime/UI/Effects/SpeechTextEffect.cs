using System;
using LitMotion;
using LitMotion.Extensions;
using TMPro;

public class SpeechTextEffect : EffectBase
{
    private TMP_Text _text;
    private string _initialTextValue;
    private IDisposable _disposable;

    private void OnEnable()
    {
        if (_text == null)
        {
            _text = GetComponent<TMP_Text>();
            _initialTextValue = _text.text;
        }

        Play();
    }

    private void Play()
    {
        _disposable?.Dispose();
        var t = _initialTextValue.Length * .025f;
        var scheduler = UnscaledTime ? MotionScheduler.UpdateIgnoreTimeScale : MotionScheduler.Update;
        _disposable = LMotion.String.Create512Bytes(string.Empty, _initialTextValue, t)
            .WithScheduler(scheduler)
            .BindToText(_text)
            .ToDisposable();
    }
}