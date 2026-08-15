// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global

using System;
using StatefulUI.Runtime.Core;
using StatefulUI.Runtime.Localization;
using StatefulUI.Runtime.References;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StatefulUISupport.Scripts.Components
{
    public static class StatefulComponentExtensions
    {
        public static AnimatorReference GetAnimator(this StatefulComponent view, AnimatorRole role) 
            => view.GetItem<AnimatorReference>((int)role);

        public static Button GetButton(this StatefulComponent view, ButtonRole role) 
            => view.GetItem<ButtonReference>((int)role).Button;

        public static ContainerReference GetContainer(this StatefulComponent view, ContainerRole role) 
            => view.GetItem<ContainerReference>((int)role);

        public static DropdownReference GetDropdown(this StatefulComponent view, DropdownRole role) 
            => view.GetItem<DropdownReference>((int)role);

        public static Image GetImage(this StatefulComponent view, ImageRole role) 
            => view.GetItem<ImageReference>((int)role).Image;

        public static InnerComponentReference GetInnerComponentReference(this StatefulComponent view, InnerComponentRole role) 
            => view.GetItem<InnerComponentReference>((int)role);

        public static StatefulComponent GetInnerComponent(this StatefulComponent view, InnerComponentRole role) 
            => view.GetItem<InnerComponentReference>((int)role).InnerComponent;

        public static ObjectReference GetObject(this StatefulComponent view, ObjectRole role) 
            => view.GetItem<ObjectReference>((int)role);

        public static SliderReference GetSlider(this StatefulComponent view, SliderRole role) 
            => view.GetItem<SliderReference>((int)role);

        public static TextInputReference GetTextInput(this StatefulComponent view, TextInputRole role) 
            => view.GetItem<TextInputReference>((int)role);

        public static TextReference GetText(this StatefulComponent view, TextRole role) 
            => view.GetItem<TextReference>((int)role);

        public static ToggleReference GetToggle(this StatefulComponent view, ToggleRole role) 
            => view.GetItem<ToggleReference>((int)role);

        public static VideoPlayerReference GetVideoPlayer(this StatefulComponent view, VideoPlayerRole role) 
            => view.GetItem<VideoPlayerReference>((int)role);

        public static bool HasButton(this StatefulComponent view, ButtonRole role) 
            => view.HasItem<ButtonReference>((int)role);

        public static bool HasContainer(this StatefulComponent view, ContainerRole role) 
            => view.HasItem<ContainerReference>((int)role);

        public static bool HasDropdown(this StatefulComponent view, DropdownRole role) 
            => view.HasItem<DropdownReference>((int)role);

        public static bool HasImage(this StatefulComponent view, ImageRole role) 
            => view.HasItem<ImageReference>((int)role);

        public static bool HasInnerComponent(this StatefulComponent view, InnerComponentRole role) 
            => view.HasItem<InnerComponentReference>((int)role);

        public static bool HasObject(this StatefulComponent view, ObjectRole role) 
            => view.HasItem<ObjectReference>((int)role);

        public static bool HasSlider(this StatefulComponent view, SliderRole role) 
            => view.HasItem<SliderReference>((int)role);

        public static bool HasTextInput(this StatefulComponent view, TextInputRole role) 
            => view.HasItem<TextInputReference>((int)role);

        public static bool HasText(this StatefulComponent view, TextRole role) 
            => view.HasItem<TextReference>((int)role);

        public static bool HasToggle(this StatefulComponent view, ToggleRole role) 
            => view.HasItem<ToggleReference>((int)role);

        public static bool HasVideoPlayer(this StatefulComponent view, VideoPlayerRole role) 
            => view.HasItem<VideoPlayerReference>((int)role);

        public static bool HasAnimator(this StatefulComponent view, AnimatorRole role) 
            => view.HasItem<AnimatorReference>((int)role);

        public static void SetText(this StatefulComponent view, TextRole role, string text)
            => view.SetRawTextByRole((int)role, text);

        public static void SetTextValues(this StatefulComponent view, TextRole role, params string[] args)
            => view.SetRawTextValuesByRole((int)role, args);

        public static void SetFormattedText(this StatefulComponent view, TextRole role, params string[] args)
            => view.SetFormattedTextByRawRole((int)role, args);

        public static void SetLocalizationText(this StatefulComponent view, TextRole role, string key)
        {
            var localization = LocalizationUtils.GetPhrase(key, string.Empty);
            view.SetRawTextByRole((int)role, localization);
        }

        public static void SetImage(this StatefulComponent view, ImageRole role, string spritePath) 
            => view.SetImageByRawRole((int)role, spritePath);

        public static void SetImage(this StatefulComponent view, ImageRole role, Sprite sprite) 
            => view.SetImageByRawRole((int)role, sprite);

        public static void SetImage(this StatefulComponent view, ImageRole role, IconInfo iconInfo)
        {
            var image = view.GetImage(role);
            image.sprite = iconInfo.Sprite;
            image.rectTransform.localScale = Vector3.one * iconInfo.ScaleMultiplier;
            image.rectTransform.anchoredPosition = iconInfo.Offset;
        }

        public static void SetButtonListener(this StatefulComponent view, ButtonRole role, UnityAction listener)
            => view.SetButtonListenerByRawRole((int)role, listener);

        public static void SetState(this StatefulComponent view, StateRole role)
            => view.SetState((int)role);

    }
}

[Serializable]
public struct IconInfo
{
    public Sprite Sprite;
    public float ScaleMultiplier;
    public Vector2 Offset;
}