#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using TransformHandles.Utils;
using Object = UnityEngine.Object;

namespace TransformHandles.Editor
{
    /// <summary>Explicit editor authoring utility. Never runs during import or in players.</summary>
    public static class PrecisionHandleAssetBuilder
    {
        private const string Root = "Packages/com.orkunmanap.runtime-transform-handles/Runtime/";
        private static readonly Color[] Colors = { new Color(.96f,.48f,.51f), new Color(.57f,.85f,.55f), new Color(.48f,.66f,.93f) };

        public static void Build()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Shader/PrecisionHandle.shader");
            if (shader == null) throw new InvalidOperationException("Precision shader must be imported first.");
            var originalTube = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Models/tube.asset");

            var tube = SaveMesh(MeshUtils.CreateTube(originalTube.bounds.size.y, 16, .008f, .008f, .008f, .008f), "PrecisionTube");
            var cone = SaveMesh(MeshUtils.CreateCone(.45f, .176f, 0, 24, 1), "PrecisionCone");
            var ring = SaveMesh(MeshUtils.CreateTorus(1, .009f, 128, 8), "PrecisionRing");
            var ringBacking = SaveMesh(MeshUtils.CreateTorus(1, .016f, 128, 8), "PrecisionRingBacking");
            var pad = SaveMesh(CreateRoundedPad(), "PrecisionPlanePad");
            var backing = SaveMaterial(shader, "PrecisionBacking", new Color(.16f,.19f,.23f,1), 3100);
            var center = SaveMaterial(shader, "PrecisionCenter", new Color(.84f,.90f,.94f,1), 3140);
            var arc = SaveMaterial(shader, "PrecisionArc", new Color(.65f,.80f,.85f,.14f), 3093);
            var materials = new Material[9];
            var planes = new Material[3];
            for (var i=0; i<9; i++) materials[i] = SaveMaterial(shader, "Precision" + (i<3?"Move":i<6?"Rotate":"Scale") + "XYZ"[i%3], Colors[i%3], 3120+i);
            for (var i = 0; i < 3; i++)
            {
                var color = Colors[i];
                color.a = .32f;
                planes[i] = SaveMaterial(shader, "PrecisionPlane" + "XYZ"[i], color, 3090 + (i + 2) % 3);
                planes[i].SetFloat("_PlaneBorderWidth", .045f);
                planes[i].SetFloat("_PlaneBorderOpacity", .95f);
            }
            var go = PrefabUtility.LoadPrefabContents(Root + "Prefabs/NativeTransformHandle.prefab");
            try
            {
                go.name = "PrecisionTransformHandle";
                foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    var position = renderer.GetComponentInParent<PositionAxis>();
                    var rotation = renderer.GetComponentInParent<RotationAxis>();
                    var scale = renderer.GetComponentInParent<ScaleAxis>();
                    var plane = renderer.GetComponentInParent<PositionPlane>();
                    var axis = position != null ? position.transform : rotation != null ? rotation.transform : scale != null ? scale.transform : null;
                    var index = axis == null || plane != null ? -1 : "XYZ".IndexOf(axis.name, StringComparison.Ordinal);
                    if (index >= 0)
                    {
                        var offset = position != null ? 0 : rotation != null ? 3 : 6;
                        renderer.sharedMaterial = materials[offset+index];
                        var owner = (Object)position ?? (Object)rotation ?? scale;
                        var serialized = new SerializedObject(owner);
                        serialized.FindProperty("defaultColor").colorValue = Colors[index];
                        if (rotation != null) serialized.FindProperty("arcMaterial").objectReferenceValue = arc;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        if (renderer.name == "Line") filter.sharedMesh = tube;
                        else if (renderer.name == "Cone") filter.sharedMesh = cone;
                        else if (renderer.name == "Torus") filter.sharedMesh = ring;
                        else if (renderer.name == "Cube")
                        {
                            var ratio = .55f;
                            renderer.transform.localScale *= ratio;
                            var collider = renderer.GetComponent<BoxCollider>();
                            if (collider != null) collider.size /= ratio;
                        }
                        AddBacking(renderer, backing, ringBacking);
                    }
                    else if (plane != null)
                    {
                        var i = "XYZ".IndexOf(plane.transform.parent.name, StringComparison.Ordinal);
                        if (i < 0) i = 0;
                        renderer.sharedMaterial = planes[i];
                        var so = new SerializedObject(plane);
                        so.FindProperty("defaultColor").colorValue = planes[i].color;
                        so.FindProperty("planeVisualOffset").floatValue = .14f;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        filter.sharedMesh = pad;
                        // The mesh lies in XZ, matching the original thin cube and its rotation.
                        renderer.transform.localScale = new Vector3(.18f, .01f, .18f);
                        var collider = renderer.GetComponent<BoxCollider>();
                        collider.center = Vector3.zero;
                        collider.size = new Vector3(.20f / .18f, 1f, .20f / .18f);
                    }
                    else
                    {
                        renderer.sharedMaterial = center;
                        var global = renderer.GetComponentInParent<ScaleGlobal>();
                        if (global != null)
                        {
                            var so = new SerializedObject(global);
                            so.FindProperty("defaultColor").colorValue = center.color;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                        // Keep the original uniform-scale collider dimensions.
                        if (renderer.name == "Cube")
                        {
                            renderer.transform.localScale *= .65f;
                            var collider = renderer.GetComponent<BoxCollider>();if(collider!=null)collider.size/=.65f;
                        }
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/PrecisionTransformHandle.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            var manager = PrefabUtility.LoadPrefabContents(Root + "Resources/TransformHandleManager.prefab");
            try
            {
                var so = new SerializedObject(manager.GetComponent<TransformHandleManager>());
                so.FindProperty("transformHandlePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PrecisionTransformHandle.prefab");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(manager, Root + "Resources/TransformHandleManager.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(manager); }
            AssetDatabase.SaveAssets();
            Debug.Log("PRECISION_ASSETS_BUILT");
        }

        private static Mesh CreateRoundedPad()
        {
            const int segmentsPerCorner = 6;
            const int perimeterCount = 4 * (segmentsPerCorner + 1);
            const float radius = .16f;
            var vertices = new Vector3[perimeterCount + 1];
            var triangles = new int[perimeterCount * 3];
            for (var corner = 0; corner < 4; corner++)
            {
                var angle = corner * Mathf.PI * .5f;
                var center = new Vector3(Mathf.Cos(angle + Mathf.PI * .25f), 0,
                    Mathf.Sin(angle + Mathf.PI * .25f)) * ((.5f - radius) * Mathf.Sqrt(2));
                for (var segment = 0; segment <= segmentsPerCorner; segment++)
                {
                    var index = corner * (segmentsPerCorner + 1) + segment;
                    var theta = angle + segment * Mathf.PI * .5f / segmentsPerCorner;
                    vertices[index + 1] = center + new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)) * radius;
                    triangles[index * 3] = 0;
                    triangles[index * 3 + 1] = (index + 1) % perimeterCount + 1;
                    triangles[index * 3 + 2] = index + 1;
                }
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBacking(MeshRenderer body, Material material, Mesh ringBacking)
        {
            var go = new GameObject("Contrast backing", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = body.gameObject.layer; go.transform.SetParent(body.transform, false);
            // Keep shafts' longitudinal reach while expanding their cross section.
            go.transform.localScale = body.name == "Torus" ? Vector3.one : body.name == "Line" ? new Vector3(1.35f,1,1.35f) : Vector3.one * 1.04f;
            go.GetComponent<MeshFilter>().sharedMesh = body.name == "Torus" ? ringBacking : body.GetComponent<MeshFilter>().sharedMesh;
            var renderer = go.GetComponent<MeshRenderer>();renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            mesh.name=name;var path=Root+"Models/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
        }

        private static Material SaveMaterial(Shader shader,string name,Color color,int queue)
        {
            var path=Root+"Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;material.name=name;material.color=color;material.renderQueue=queue;EditorUtility.SetDirty(material);return material;
        }
    }
}
#endif
