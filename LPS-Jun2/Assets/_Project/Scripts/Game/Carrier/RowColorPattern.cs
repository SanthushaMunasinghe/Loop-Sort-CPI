using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RowColorPattern", menuName = "Carriers/Row Color Pattern")]
public sealed class RowColorPattern : ScriptableObject
{
    [Tooltip("Compatible colors assigned to a row's sink carriers, front-to-back. Wraps back to the " +
             "first entry if the row has more carriers than this has entries.")]
    [SerializeField] private ColorType[] _colors;

    public IReadOnlyList<ColorType> Colors => _colors;
}
