using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SceneInspector
{
    [MenuItem("Tools/Inspect Scene")]
    public static void Inspect()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        string reportPath = "scene_inspection_report.txt";
        using (StreamWriter writer = new StreamWriter(reportPath, false))
        {
            writer.WriteLine("Scene Name: " + scene.name);
            writer.WriteLine("Total Root GameObjects: " + scene.rootCount);
            writer.WriteLine("========================================");

            var rootObjects = scene.GetRootGameObjects();
            foreach (var rootObj in rootObjects)
            {
                InspectGameObject(rootObj, "", writer);
            }
        }
        Debug.Log("Scene inspection completed. Report written to " + reportPath);
    }

    private static void InspectGameObject(GameObject obj, string indent, StreamWriter writer)
    {
        writer.WriteLine($"{indent}- Name: {obj.name}");
        writer.WriteLine($"{indent}  Active: {obj.activeSelf}");
        writer.WriteLine($"{indent}  Tag: {obj.tag}");
        writer.WriteLine($"{indent}  Layer: {LayerMask.LayerToName(obj.layer)}");
        
        var t = obj.transform;
        writer.WriteLine($"{indent}  Position: {t.localPosition}, Rotation: {t.localEulerAngles}, Scale: {t.localScale}");

        var components = obj.GetComponents<Component>();
        writer.Write($"{indent}  Components: ");
        foreach (var comp in components)
        {
            if (comp != null)
            {
                writer.Write(comp.GetType().Name + " ");
            }
        }
        writer.WriteLine();

        // Mesh information
        var meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            writer.WriteLine($"{indent}  Mesh: {meshFilter.sharedMesh.name}");
        }

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            writer.Write($"{indent}  Materials: ");
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat != null)
                {
                    writer.Write(mat.name + " ");
                }
            }
            writer.WriteLine();
        }

        writer.WriteLine($"{indent}----------------------------------------");

        for (int i = 0; i < t.childCount; i++)
        {
            InspectGameObject(t.GetChild(i).gameObject, indent + "  ", writer);
        }
    }
}
