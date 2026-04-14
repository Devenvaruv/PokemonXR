#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HierarchyEverythingDump
{
    // ====== SETTINGS ======
    private const int MAX_STRING_LENGTH = 20000;          // Safety limit for huge strings
    private const int MAX_ARRAY_ELEMENTS = 5000;          // Safety for massive arrays/lists
    private const bool INCLUDE_NON_PUBLIC_SERIALIZED = true; // Unity serializes some non-public fields
    private const bool INCLUDE_HIDE_IN_INSPECTOR = true;     // Properties that are serialized but hidden
    private const bool INCLUDE_TRANSFORM_WORLD = true;       // Adds world pos/rot/scale

    [MenuItem("Tools/Agent/Dump EVERYTHING (Hierarchy+Components+Serialized)")]
    public static void DumpEverything()
    {
        // Encourage saving for stable GlobalObjectIds
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            return;

        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        var path = EditorUtility.SaveFilePanel(
            "Save Full Hierarchy Dump JSON",
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
            $"{scene.name}_FullDump_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            "json"
        );
        if (string.IsNullOrEmpty(path)) return;

        var sb = new StringBuilder(1024 * 1024);
        var ctx = new Context();

        sb.Append("{");
        WriteKV(sb, "schemaVersion", "1.0"); sb.Append(",");
        WriteKV(sb, "unityVersion", Application.unityVersion); sb.Append(",");
        WriteKV(sb, "sceneName", scene.name); sb.Append(",");
        WriteKV(sb, "scenePath", scene.path); sb.Append(",");
        WriteKV(sb, "exportedAtUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)); sb.Append(",");
        sb.Append("\"roots\":[");
        for (int i = 0; i < roots.Length; i++)
        {
            if (i > 0) sb.Append(",");
            WriteGameObject(sb, roots[i], parentPath: null, ctx);
        }
        sb.Append("]");
        sb.Append("}");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Debug.Log($"[AgentDump] Wrote full dump to: {path}");
        EditorUtility.RevealInFinder(path);
    }

    // ====== INTERNALS ======

    private sealed class Context
    {
        // Helps keep hier paths unique when siblings share names
        public readonly Dictionary<Transform, Dictionary<string, int>> nameCounters = new();
    }

    private static void WriteGameObject(StringBuilder sb, GameObject go, string parentPath, Context ctx)
    {
        var t = go.transform;
        string seg = MakeUniqueSegment(t, ctx);
        string hierPath = string.IsNullOrEmpty(parentPath) ? seg : $"{parentPath}/{seg}";

        sb.Append("{");

        WriteKV(sb, "name", go.name); sb.Append(",");
        WriteKV(sb, "activeSelf", go.activeSelf); sb.Append(",");
        WriteKV(sb, "activeInHierarchy", go.activeInHierarchy); sb.Append(",");
        WriteKV(sb, "tag", go.tag); sb.Append(",");
        WriteKV(sb, "layer", go.layer); sb.Append(",");
        WriteKV(sb, "isStatic", go.isStatic); sb.Append(",");
        WriteKV(sb, "scenePath", go.scene.path); sb.Append(",");
        WriteKV(sb, "hierPath", hierPath); sb.Append(",");

        // Stable ID for scene objects
        WriteKV(sb, "globalId", GetGlobalId(go)); sb.Append(",");
        WriteKV(sb, "instanceId", go.GetInstanceID()); sb.Append(",");

        // Transform block
        sb.Append("\"transform\":{");
        WriteVector3(sb, "localPosition", t.localPosition); sb.Append(",");
        WriteQuaternion(sb, "localRotation", t.localRotation); sb.Append(",");
        WriteVector3(sb, "localScale", t.localScale);

        if (INCLUDE_TRANSFORM_WORLD)
        {
            sb.Append(",");
            WriteVector3(sb, "position", t.position); sb.Append(",");
            WriteQuaternion(sb, "rotation", t.rotation); sb.Append(",");
            // lossyScale can be weird but useful
            WriteVector3(sb, "lossyScale", t.lossyScale);
        }
        sb.Append("},"); // end transform

        // Components block
        sb.Append("\"components\":[");
        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            if (i > 0) sb.Append(",");
            WriteComponent(sb, comps[i], go);
        }
        sb.Append("],");

        // Children block
        sb.Append("\"children\":[");
        for (int i = 0; i < t.childCount; i++)
        {
            if (i > 0) sb.Append(",");
            WriteGameObject(sb, t.GetChild(i).gameObject, hierPath, ctx);
        }
        sb.Append("]");

        sb.Append("}");
    }

    private static void WriteComponent(StringBuilder sb, Component c, GameObject owner)
    {
        if (c == null)
        {
            sb.Append("{");
            WriteKV(sb, "missingScript", true);
            sb.Append("}");
            return;
        }

        var type = c.GetType();
        sb.Append("{");
        WriteKV(sb, "type", type.FullName); sb.Append(",");
        WriteKV(sb, "instanceId", c.GetInstanceID()); sb.Append(",");
        WriteKV(sb, "globalId", GetGlobalId(c)); sb.Append(",");
        WriteKV(sb, "missingScript", false);

        // MonoBehaviour extra info (script GUID is the key for “agent-friendly” mapping)
        if (c is MonoBehaviour mb)
        {
            sb.Append(",");
            WriteKV(sb, "isMonoBehaviour", true); sb.Append(",");
            var ms = MonoScript.FromMonoBehaviour(mb);
            var scriptPath = ms ? AssetDatabase.GetAssetPath(ms) : null;
            var guid = !string.IsNullOrEmpty(scriptPath) ? AssetDatabase.AssetPathToGUID(scriptPath) : null;
            WriteKV(sb, "scriptGuid", guid); sb.Append(",");
            WriteKV(sb, "scriptPath", scriptPath); sb.Append(",");
            WriteKV(sb, "scriptClass", ms ? ms.GetClass()?.FullName : null);
        }
        else
        {
            sb.Append(",");
            WriteKV(sb, "isMonoBehaviour", false);
        }

        // Serialized properties (the “everything” part)
        sb.Append(",");
        sb.Append("\"serialized\":");
        WriteSerializedObject(sb, c);

        sb.Append("}");
    }

    private static void WriteSerializedObject(StringBuilder sb, UnityEngine.Object obj)
    {
        // Some UnityEngine.Objects can fail serialization (rare). Guard it.
        SerializedObject so = null;
        try { so = new SerializedObject(obj); }
        catch
        {
            sb.Append("null");
            return;
        }

        var it = so.GetIterator();
        bool enterChildren = true;

        sb.Append("{");
        bool firstProp = true;

        while (it.NextVisible(enterChildren))
        {
            enterChildren = true;

            // if (!INCLUDE_HIDE_IN_INSPECTOR && it.isHidden)
            //     continue;

            // Unity includes "m_Script" for MonoBehaviours; keep it (it’s useful)
            // Key by propertyPath so nested fields are uniquely addressable.
            string key = it.propertyPath;

            if (!firstProp) sb.Append(",");
            firstProp = false;

            WriteJSONString(sb, key);
            sb.Append(":");
            WriteSerializedValue(sb, it);
        }

        sb.Append("}");
    }

    private static void WriteSerializedValue(StringBuilder sb, SerializedProperty p)
    {
        // Handle arrays carefully
        if (p.isArray && p.propertyType != SerializedPropertyType.String)
        {
            int n = Math.Min(p.arraySize, MAX_ARRAY_ELEMENTS);
            sb.Append("{");
            WriteKV(sb, "type", "Array"); sb.Append(",");
            WriteKV(sb, "arraySize", p.arraySize); sb.Append(",");
            sb.Append("\"elements\":[");
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(",");
                var el = p.GetArrayElementAtIndex(i);
                WriteSerializedValue(sb, el);
            }
            sb.Append("]");
            if (p.arraySize > n)
            {
                sb.Append(",");
                WriteKV(sb, "truncated", true);
                sb.Append(",");
                WriteKV(sb, "maxElements", MAX_ARRAY_ELEMENTS);
            }
            sb.Append("}");
            return;
        }

        // Most primitives + structs + references:
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer:
                sb.Append(p.longValue.ToString(CultureInfo.InvariantCulture));
                return;

            case SerializedPropertyType.Boolean:
                sb.Append(p.boolValue ? "true" : "false");
                return;

            case SerializedPropertyType.Float:
                // Covers float/double in Unity's serialized system
                sb.Append(p.doubleValue.ToString("R", CultureInfo.InvariantCulture));
                return;

            case SerializedPropertyType.String:
                WriteJSONString(sb, Truncate(p.stringValue, MAX_STRING_LENGTH));
                return;

            case SerializedPropertyType.Color:
                WriteColor(sb, p.colorValue);
                return;

            case SerializedPropertyType.ObjectReference:
                WriteObjectReference(sb, p.objectReferenceValue);
                return;

            case SerializedPropertyType.LayerMask:
                sb.Append(p.intValue.ToString(CultureInfo.InvariantCulture));
                return;

            case SerializedPropertyType.Enum:
                sb.Append("{");
                WriteKV(sb, "enumValueIndex", p.enumValueIndex); sb.Append(",");
                WriteKV(sb, "enumNames", p.enumDisplayNames);
                sb.Append("}");
                return;

            case SerializedPropertyType.Vector2:
                WriteVector2(sb, p.vector2Value);
                return;

            case SerializedPropertyType.Vector3:
                WriteVector3Value(sb, p.vector3Value);
                return;

            case SerializedPropertyType.Vector4:
                WriteVector4(sb, p.vector4Value);
                return;

            case SerializedPropertyType.Rect:
                WriteRect(sb, p.rectValue);
                return;

            case SerializedPropertyType.Bounds:
                WriteBounds(sb, p.boundsValue);
                return;

            case SerializedPropertyType.Quaternion:
                WriteQuaternionValue(sb, p.quaternionValue);
                return;

            case SerializedPropertyType.Character:
                sb.Append(((int)p.intValue).ToString(CultureInfo.InvariantCulture));
                return;

            case SerializedPropertyType.AnimationCurve:
                // Curves can be huge; keep summary + keyframes count
                var c = p.animationCurveValue;
                sb.Append("{");
                WriteKV(sb, "type", "AnimationCurve"); sb.Append(",");
                WriteKV(sb, "keys", c != null ? c.keys.Length : 0);
                sb.Append("}");
                return;

            case SerializedPropertyType.Gradient:
                // Unity doesn't expose gradient details reliably across versions; summarize
                sb.Append("{");
                WriteKV(sb, "type", "Gradient");
                sb.Append("}");
                return;

            case SerializedPropertyType.ExposedReference:
                // Similar to ObjectReference
                WriteObjectReference(sb, p.exposedReferenceValue);
                return;

            case SerializedPropertyType.FixedBufferSize:
                sb.Append("{");
                WriteKV(sb, "type", "FixedBuffer"); sb.Append(",");
                WriteKV(sb, "size", p.fixedBufferSize);
                sb.Append("}");
                return;

            case SerializedPropertyType.ManagedReference:
                // Managed references can be deep; capture typename + json-ish via Copy?
                sb.Append("{");
                WriteKV(sb, "type", "ManagedReference"); sb.Append(",");
                WriteKV(sb, "managedReferenceFullTypename", p.managedReferenceFullTypename);
                sb.Append("}");
                return;

            case SerializedPropertyType.Generic:
            default:
                // For nested structs/classes, serialize children by iterating them
                // but WITHOUT expanding invisible properties unless requested.
                WriteGenericPropertyObject(sb, p);
                return;
        }
    }

    private static void WriteGenericPropertyObject(StringBuilder sb, SerializedProperty p)
    {
        // For Generic, we can attempt to enumerate children by depth.
        sb.Append("{");
        WriteKV(sb, "type", "Generic"); sb.Append(",");
        WriteKV(sb, "propertyType", p.propertyType.ToString()); sb.Append(",");
        sb.Append("\"children\":{");

        bool first = true;
        var copy = p.Copy();
        var end = copy.GetEndProperty();
        bool enterChildren = true;

        // Move to first child
        if (!copy.NextVisible(enterChildren))
        {
            sb.Append("}}");
            return;
        }

        while (!SerializedProperty.EqualContents(copy, end))
        {
            // Only include direct children of this property (depth = parentDepth+1)
            if (copy.depth == p.depth + 1)
            {
                if (!first) sb.Append(",");
                first = false;

                WriteJSONString(sb, copy.name);
                sb.Append(":");
                WriteSerializedValue(sb, copy);
            }

            if (!copy.NextVisible(true))
                break;
        }

        sb.Append("}"); // end children object
        sb.Append("}"); // end generic object
    }

    // ====== OBJECT REFS ======

    private static void WriteObjectReference(StringBuilder sb, UnityEngine.Object o)
    {
        if (o == null)
        {
            sb.Append("null");
            return;
        }

        sb.Append("{");
        WriteKV(sb, "name", o.name); sb.Append(",");
        WriteKV(sb, "type", o.GetType().FullName); sb.Append(",");
        WriteKV(sb, "instanceId", o.GetInstanceID()); sb.Append(",");

        // If asset, store GUID/path
        var path = AssetDatabase.GetAssetPath(o);
        if (!string.IsNullOrEmpty(path))
        {
            WriteKV(sb, "refKind", "asset"); sb.Append(",");
            WriteKV(sb, "assetPath", path); sb.Append(",");
            WriteKV(sb, "assetGuid", AssetDatabase.AssetPathToGUID(path));
        }
        else
        {
            // Scene object/component: use GlobalObjectId if possible
            WriteKV(sb, "refKind", "scene"); sb.Append(",");
            WriteKV(sb, "globalId", GetGlobalId(o));
        }

        sb.Append("}");
    }

    private static string GetGlobalId(UnityEngine.Object obj)
    {
        try
        {
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            return gid.ToString();
        }
        catch
        {
            return null;
        }
    }

    // ====== UNIQUE HIER SEGMENTS ======

    private static string MakeUniqueSegment(Transform t, Context ctx)
    {
        Transform parent = t.parent;
        string baseName = t.gameObject.name;

        if (parent == null)
        {
            // Root: index among roots with same name
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            int idx = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == t.gameObject) break;
                if (roots[i].name == baseName) idx++;
            }
            return $"{baseName}[{idx}]";
        }

        if (!ctx.nameCounters.TryGetValue(parent, out var map))
        {
            map = new Dictionary<string, int>();
            ctx.nameCounters[parent] = map;
        }

        // Determine index among siblings with the same name by scanning parent's children
        int index = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var sib = parent.GetChild(i);
            if (sib == t) break;
            if (sib.gameObject.name == baseName) index++;
        }

        return $"{baseName}[{index}]";
    }

    // ====== JSON HELPERS ======

    private static void WriteKV(StringBuilder sb, string key, string value)
    {
        WriteJSONString(sb, key);
        sb.Append(":");
        if (value == null) sb.Append("null");
        else WriteJSONString(sb, value);
    }

    private static void WriteKV(StringBuilder sb, string key, bool value)
    {
        WriteJSONString(sb, key);
        sb.Append(":");
        sb.Append(value ? "true" : "false");
    }

    private static void WriteKV(StringBuilder sb, string key, int value)
    {
        WriteJSONString(sb, key);
        sb.Append(":");
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteKV(StringBuilder sb, string key, long value)
    {
        WriteJSONString(sb, key);
        sb.Append(":");
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteKV(StringBuilder sb, string key, string[] values)
    {
        WriteJSONString(sb, key);
        sb.Append(":");
        if (values == null) { sb.Append("null"); return; }
        sb.Append("[");
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append(",");
            WriteJSONString(sb, values[i]);
        }
        sb.Append("]");
    }

    private static void WriteKV(StringBuilder sb, string key, IList<string> values)
    {
        WriteJSONString(sb, key);
        sb.Append(":");
        if (values == null) { sb.Append("null"); return; }
        sb.Append("[");
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(",");
            WriteJSONString(sb, values[i]);
        }
        sb.Append("]");
    }

    private static void WriteVector2(StringBuilder sb, Vector2 v)
    {
        sb.Append("{\"x\":"); sb.Append(v.x.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"y\":"); sb.Append(v.y.ToString("R", CultureInfo.InvariantCulture));
        sb.Append("}");
    }

    private static void WriteVector3Value(StringBuilder sb, Vector3 v)
    {
        sb.Append("{\"x\":"); sb.Append(v.x.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"y\":"); sb.Append(v.y.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"z\":"); sb.Append(v.z.ToString("R", CultureInfo.InvariantCulture));
        sb.Append("}");
    }

    private static void WriteVector3(StringBuilder sb, string key, Vector3 v)
    {
        WriteJSONString(sb, key);
        sb.Append(":");
        WriteVector3Value(sb, v);
    }

    private static void WriteVector4(StringBuilder sb, Vector4 v)
    {
        sb.Append("{\"x\":"); sb.Append(v.x.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"y\":"); sb.Append(v.y.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"z\":"); sb.Append(v.z.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"w\":"); sb.Append(v.w.ToString("R", CultureInfo.InvariantCulture));
        sb.Append("}");
    }

    private static void WriteQuaternionValue(StringBuilder sb, Quaternion q)
    {
        sb.Append("{\"x\":"); sb.Append(q.x.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"y\":"); sb.Append(q.y.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"z\":"); sb.Append(q.z.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"w\":"); sb.Append(q.w.ToString("R", CultureInfo.InvariantCulture));
        sb.Append("}");
    }

    private static void WriteQuaternion(StringBuilder sb, string key, Quaternion q)
    {
        WriteJSONString(sb, key);
        sb.Append(":");
        WriteQuaternionValue(sb, q);
    }

    private static void WriteColor(StringBuilder sb, Color c)
    {
        sb.Append("{\"r\":"); sb.Append(c.r.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"g\":"); sb.Append(c.g.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"b\":"); sb.Append(c.b.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"a\":"); sb.Append(c.a.ToString("R", CultureInfo.InvariantCulture));
        sb.Append("}");
    }

    private static void WriteRect(StringBuilder sb, Rect r)
    {
        sb.Append("{\"x\":"); sb.Append(r.x.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"y\":"); sb.Append(r.y.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"w\":"); sb.Append(r.width.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(",\"h\":"); sb.Append(r.height.ToString("R", CultureInfo.InvariantCulture));
        sb.Append("}");
    }

    private static void WriteBounds(StringBuilder sb, Bounds b)
    {
        sb.Append("{\"center\":");
        WriteVector3Value(sb, b.center);
        sb.Append(",\"extents\":");
        WriteVector3Value(sb, b.extents);
        sb.Append("}");
    }

    private static void WriteJSONString(StringBuilder sb, string s)
    {
        if (s == null) { sb.Append("null"); return; }
        sb.Append("\"");
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 32) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    else sb.Append(ch);
                    break;
            }
        }
        sb.Append("\"");
    }

    private static string Truncate(string s, int max)
    {
        if (s == null) return null;
        if (s.Length <= max) return s;
        return s.Substring(0, max) + $"…(truncated,{s.Length} chars)";
    }
}
#endif
