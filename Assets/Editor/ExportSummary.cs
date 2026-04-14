#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HierarchySummaryExport
{
    [Serializable]
    public class SummaryDump
    {
        public string scene;
        public string generatedAt;
        public List<SummaryNode> roots = new();
    }

    [Serializable]
    public class SummaryNode
    {
        public string name;
        public string type = "GameObject";
        public List<string> components = new();
        public List<SummaryNode> children = new();
    }

    private const string DefaultConfigPath = "Assets/Editor/AgentHierarchyConfig.asset";

    [MenuItem("Tools/Agent/Export Hierarchy Summary JSON")]
    public static void ExportSummary()
    {
        var config = LoadOrCreateConfig();

        var scene = SceneManager.GetActiveScene();
        var dump = new SummaryDump
        {
            scene = scene.name,
            generatedAt = DateTime.UtcNow.ToString("o")
        };

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var go = roots[i];
            var node = Build(go, config, parentPath: "", depth: 0);
            if (node != null) dump.roots.Add(node);
        }

        // JsonUtility doesn't handle top-level Lists well unless wrapped (we did).
        var json = JsonUtility.ToJson(dump, true);

        // Write outside Assets to avoid reimport loops
        var outPath = Path.Combine(Application.dataPath, "../HierarchySummary.json");
        File.WriteAllText(outPath, json);

        Debug.Log($"[Agent] Exported hierarchy SUMMARY to: {outPath}");
        EditorUtility.RevealInFinder(outPath);
    }

    private static SummaryNode Build(GameObject go, AgentHierarchyConfig cfg, string parentPath, int depth)
    {
        var path = string.IsNullOrEmpty(parentPath) ? go.name : $"{parentPath}/{go.name}";

        // Depth limit
        if (cfg.maxDepth > 0 && depth > cfg.maxDepth) return null;

        // Skip whole subtree if GO is blacklisted
        if (IsBlacklistedGameObject(go, path, cfg)) return null;

        var node = new SummaryNode
        {
            name = go.name,
            components = GetComponentTypeNames(go, cfg)
        };

        // Children
        var t = go.transform;
        int childCount = t.childCount;

        int limit = cfg.maxChildrenPerNode > 0 ? Mathf.Min(childCount, cfg.maxChildrenPerNode) : childCount;

        for (int i = 0; i < limit; i++)
        {
            var childGo = t.GetChild(i).gameObject;
            var child = Build(childGo, cfg, path, depth + 1);
            if (child != null) node.children.Add(child);
        }

        // If we truncated children, note it via a sentinel child node (agent-friendly)
        if (cfg.maxChildrenPerNode > 0 && childCount > cfg.maxChildrenPerNode)
        {
            node.children.Add(new SummaryNode
            {
                name = $"… ({childCount - cfg.maxChildrenPerNode} more children omitted)",
                components = new List<string>(),
                children = new List<SummaryNode>()
            });
        }

        return node;
    }

    private static bool IsBlacklistedGameObject(GameObject go, string path, AgentHierarchyConfig cfg)
    {
        // exact name
        for (int i = 0; i < cfg.blacklistExactNames.Count; i++)
            if (go.name == cfg.blacklistExactNames[i]) return true;

        // contains name
        for (int i = 0; i < cfg.blacklistNameContains.Count; i++)
            if (!string.IsNullOrEmpty(cfg.blacklistNameContains[i]) && go.name.IndexOf(cfg.blacklistNameContains[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

        // contains path
        for (int i = 0; i < cfg.blacklistPathContains.Count; i++)
            if (!string.IsNullOrEmpty(cfg.blacklistPathContains[i]) && path.IndexOf(cfg.blacklistPathContains[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

        return false;
    }

    private static List<string> GetComponentTypeNames(GameObject go, AgentHierarchyConfig cfg)
    {
        var result = new List<string>();
        var comps = go.GetComponents<Component>();

        bool useWhitelist = cfg.componentWhitelist != null && cfg.componentWhitelist.Count > 0;
        int maxComps = cfg.maxComponentsPerNode;

        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;

            var typeName = c.GetType().FullName;

            // Skip Transform/RectTransform etc if blacklisted
            if (IsBlacklistedComponent(typeName, cfg)) continue;

            if (useWhitelist && !IsWhitelistedComponent(typeName, cfg)) continue;

            result.Add(ShortenTypeName(typeName));

            if (maxComps > 0 && result.Count >= maxComps)
            {
                result.Add("…");
                break;
            }
        }

        return result;
    }

    private static bool IsBlacklistedComponent(string typeName, AgentHierarchyConfig cfg)
    {
        for (int i = 0; i < cfg.blacklistComponentTypes.Count; i++)
            if (typeName == cfg.blacklistComponentTypes[i]) return true;
        return false;
    }

    private static bool IsWhitelistedComponent(string typeName, AgentHierarchyConfig cfg)
    {
        for (int i = 0; i < cfg.componentWhitelist.Count; i++)
            if (typeName == cfg.componentWhitelist[i]) return true;
        return false;
    }

    private static string ShortenTypeName(string fullName)
    {
        // Make it easier for agents/humans: "UnityEngine.UI.Button" -> "Button"
        int lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
    }

    private static AgentHierarchyConfig LoadOrCreateConfig()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<AgentHierarchyConfig>(DefaultConfigPath);
        if (cfg != null) return cfg;

        // Create folder if missing
        var editorFolder = "Assets/Editor";
        if (!AssetDatabase.IsValidFolder(editorFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Editor");
        }

        cfg = ScriptableObject.CreateInstance<AgentHierarchyConfig>();
        AssetDatabase.CreateAsset(cfg, DefaultConfigPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Agent] Created default config at {DefaultConfigPath}. Edit it in the Inspector.");
        Selection.activeObject = cfg;
        EditorGUIUtility.PingObject(cfg);

        return cfg;
    }
}
#endif
