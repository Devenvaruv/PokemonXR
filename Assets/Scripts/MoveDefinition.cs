using UnityEngine;

[System.Serializable]
public class MoveDefinition
{
    public string moveName = "Tackle";
    [Tooltip("Base damage value used by the simple battle formula.")]
    public int power = 20;
    [Range(0f, 1f)]
    [Tooltip("Chance for the move to land. 1 = always hits.")]
    public float accuracy = 0.95f;
    [Tooltip("Total PP for this move.")]
    public int maxPP = 10;
    [Tooltip("Short description shown in UI/logs.")]
    [TextArea]
    public string description = "A straightforward hit.";
}
