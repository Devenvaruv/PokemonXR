#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Agent/Hierarchy Export Config", fileName = "AgentHierarchyConfig")]
public class AgentHierarchyConfig : ScriptableObject
{
    [Header("Output Limits")]
    [Tooltip("0 = unlimited")]
    public int maxDepth = 6;

    [Tooltip("0 = unlimited")]
    public int maxChildrenPerNode = 0;

    [Header("Blacklist GameObjects")]
    [Tooltip("Exact GameObject names to skip entirely (subtree removed).")]
    public List<string> blacklistExactNames = new() { "XR Origin" };

    [Tooltip("If a GameObject name contains any of these substrings, skip it.")]
    public List<string> blacklistNameContains = new() { "XR", "OVR", "EventSystem" };

    [Tooltip("If the hierarchy path contains any of these substrings, skip it (e.g., \"UI/Debug\").")]
    public List<string> blacklistPathContains = new();

    [Header("Blacklist Components")]
    [Tooltip("Component full names to exclude from the components list (node still exported).")]
    public List<string> blacklistComponentTypes = new()
    {
        "UnityEngine.Transform",
        "UnityEngine.RectTransform"
    };

    [Header("Include Mode (Optional)")]
    [Tooltip("If non-empty, ONLY these component type names will be listed (Transform is still omitted by default).")]
    public List<string> componentWhitelist = new();

    [Header("Collapse / Simplify")]
    [Tooltip("If true, only include the first N components per object (0 = unlimited).")]
    public int maxComponentsPerNode = 6;
}
#endif
