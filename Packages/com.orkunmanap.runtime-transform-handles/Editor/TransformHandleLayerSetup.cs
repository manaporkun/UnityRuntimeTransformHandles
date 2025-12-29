#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TransformHandles.Editor
{
    /// <summary>
    /// Editor utility for setting up the TransformHandle layer.
    /// </summary>
    public static class TransformHandleLayerSetup
    {
        private const string DefaultLayerName = "TransformHandle";
        private const int PreferredLayerIndex = 6;

        [MenuItem("Tools/Transform Handles/Setup Layer")]
        public static void SetupLayer()
        {
            if (LayerExists(DefaultLayerName))
            {
                EditorUtility.DisplayDialog(
                    "Transform Handle Layer",
                    $"Layer '{DefaultLayerName}' already exists.",
                    "OK");
                return;
            }

            var success = CreateLayer(DefaultLayerName);
            if (success)
            {
                EditorUtility.DisplayDialog(
                    "Transform Handle Layer",
                    $"Layer '{DefaultLayerName}' has been created successfully.\n\n" +
                    "Make sure the TransformHandleManager's Layer Mask includes this layer.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Transform Handle Layer",
                    "Failed to create layer. All user layers may be in use.\n\n" +
                    "Please manually add a layer named 'TransformHandle' in:\n" +
                    "Edit > Project Settings > Tags and Layers",
                    "OK");
            }
        }

        [MenuItem("Tools/Transform Handles/Check Layer Status")]
        public static void CheckLayerStatus()
        {
            var layerIndex = LayerMask.NameToLayer(DefaultLayerName);
            if (layerIndex != -1)
            {
                EditorUtility.DisplayDialog(
                    "Transform Handle Layer Status",
                    $"Layer '{DefaultLayerName}' exists at index {layerIndex}.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Transform Handle Layer Status",
                    $"Layer '{DefaultLayerName}' does not exist.\n\n" +
                    "Use 'Tools > Transform Handles > Setup Layer' to create it.",
                    "OK");
            }
        }

        private static bool LayerExists(string layerName)
        {
            return LayerMask.NameToLayer(layerName) != -1;
        }

        private static bool CreateLayer(string layerName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
            var layersProperty = tagManager.FindProperty("layers");

            // Try preferred index first (6)
            if (TrySetLayer(layersProperty, PreferredLayerIndex, layerName))
            {
                tagManager.ApplyModifiedProperties();
                return true;
            }

            // Find first empty user layer (8-31)
            for (int i = 8; i < 32; i++)
            {
                if (TrySetLayer(layersProperty, i, layerName))
                {
                    tagManager.ApplyModifiedProperties();
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetLayer(SerializedProperty layersProperty, int index, string layerName)
        {
            var layerProperty = layersProperty.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layerProperty.stringValue))
            {
                layerProperty.stringValue = layerName;
                return true;
            }
            return false;
        }
    }
}
#endif
