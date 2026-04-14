#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HierarchyExportUpgraded
{
    [Serializable]
    public class ComponentInfo
    {
        public string type;          // e.g. "UnityEngine.Transform"
        public string scriptGuid;    // only for MonoBehaviours (can be null)
        public bool missing;         // true if component is missing script
    }

    [Serializable]
    public class Node
    {
        public string name;
        public bool active;

        // Unique even with duplicate names:
        // Example: "Root[0]/Enemy[2]/Weapon[0]"
        public string hierPath;

        // Stable across sessions IF scene/prefab is saved.
        // Example: "GlobalObjectId_V1-2-..."; use it as the primary key.
        public string globalId;

        public List<ComponentInfo> components = new();
        public List<Node> children = new();
    }

    [Serializable]
    public class SceneDump
    {
        public string sceneName;
        public string scenePath;
        public bool sceneIsDirty;
        public List<Node> roots = new();
    }

    [MenuItem("Tools/Agent/Export Hierarchy JSON (Upgraded)")]
    public static void Export()
    {
        var scene = SceneManager.GetActiveScene();
        var dump = new SceneDump
        {
            sceneName = scene.name,
            scenePath = scene.path,
            sceneIsDirty = scene.isDirty
        };

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            dump.roots.Add(BuildNode(root, parentPath: null));
        }

        var json = JsonUtility.ToJson(dump, true);

        // Write outside Assets so it doesn't reimport and trigger compilation loops.
        var outPath = Path.Combine(Application.dataPath, "../HierarchyDump.json");
        File.WriteAllText(outPath, json);

        Debug.Log($"[Agent] Exported hierarchy to: {outPath}");
        EditorUtility.RevealInFinder(outPath);

        if (scene.isDirty)
        {
            Debug.LogWarning("[Agent] Scene is dirty/unsaved. GlobalObjectId is best once the scene is saved.");
        }
    }

    private static Node BuildNode(GameObject go, string parentPath)
    {
        var index = GetSiblingIndexAmongSameName(go);
        var seg = $"{go.name}[{index}]";
        var hierPath = string.IsNullOrEmpty(parentPath) ? seg : $"{parentPath}/{seg}";

        var node = new Node
        {
            name = go.name,
            active = go.activeSelf,
            hierPath = hierPath,
            globalId = GetGlobalId(go)
        };

        node.components = GetComponents(go);

        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i).gameObject;
            node.children.Add(BuildNode(child, hierPath));
        }

        return node;
    }

    // Ensures uniqueness when multiple siblings share the same name.
    // Example: if there are 3 children named "Enemy", they will become Enemy[0], Enemy[1], Enemy[2].
    private static int GetSiblingIndexAmongSameName(GameObject go)
    {
        var t = go.transform;
        var parent = t.parent;
        if (parent == null)
        {
            // Root objects: compute index among root objects with same name
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            int idx = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == go) return idx;
                if (roots[i].name == go.name) idx++;
            }
            return 0;
        }
        else
        {
            int idx = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                var sib = parent.GetChild(i).gameObject;
                if (sib == go) return idx;
                if (sib.name == go.name) idx++;
            }
            return 0;
        }
    }

    private static string GetGlobalId(UnityEngine.Object obj)
    {
        try
        {
            // Best persistent id Unity exposes in editor.
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            return gid.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static List<ComponentInfo> GetComponents(GameObject go)
    {
        var list = new List<ComponentInfo>();
        var comps = go.GetComponents<Component>();

        foreach (var c in comps)
        {
            if (c == null)
            {
                list.Add(new ComponentInfo { type = "(Missing Script)", missing = true });
                continue;
            }

            var info = new ComponentInfo
            {
                type = c.GetType().FullName,
                missing = false
            };

            // If it's a MonoBehaviour, capture the script GUID (stable identifier for the script file).
            if (c is MonoBehaviour mb)
            {
                var script = MonoScript.FromMonoBehaviour(mb);
                if (script != null)
                {
                    var path = AssetDatabase.GetAssetPath(script);
                    if (!string.IsNullOrEmpty(path))
                    {
                        info.scriptGuid = AssetDatabase.AssetPathToGUID(path);
                    }
                }
            }

            list.Add(info);
        }

        return list;
    }
}
#endif
