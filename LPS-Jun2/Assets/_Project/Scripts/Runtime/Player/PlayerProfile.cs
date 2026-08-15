using System;
using UnityEngine;

[Serializable]
public struct PlayerProfile
{
    public string Nickname;
    public string AvatarId;

    public override string ToString() => JsonUtility.ToJson(this);
    public static readonly PlayerProfile Default = new() { Nickname = "Player", AvatarId = "default" };
}