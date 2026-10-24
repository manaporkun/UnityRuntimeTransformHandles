using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TransformHandles.Tests.Editor
{
    public class PrecisionPrefabTests
    {
        private const string Root = "Packages/com.orkunmanap.runtime-transform-handles/Runtime/";

        [Test]
        public void DefaultUsesPrecisionAndClassicRemainsAvailable()
        {
            var manager = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Resources/TransformHandleManager.prefab");
            var serialized = new SerializedObject(manager.GetComponent<TransformHandleManager>());
            Assert.AreEqual(Root + "Prefabs/PrecisionTransformHandle.prefab",
                AssetDatabase.GetAssetPath(serialized.FindProperty("transformHandlePrefab").objectReferenceValue));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/NativeTransformHandle.prefab"));
        }

        [Test]
        public void PrecisionPreservesAxisPickingAndAlignsCompactPlaneVolumes()
        {
            var classic = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/NativeTransformHandle.prefab"));
            var precision = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PrecisionTransformHandle.prefab"));
            try
            {
                var expected = classic.GetComponentsInChildren<Collider>(true);
                var actual = precision.GetComponentsInChildren<Collider>(true);
                Assert.AreEqual(expected.Length, actual.Length);
                for (var i = 0; i < expected.Length; i++)
                {
                    var a = expected[i]; var b = actual[i];
                    Assert.AreEqual(a.GetType(), b.GetType());
                    Assert.AreEqual(a.name, b.name);
                    if (a.GetComponentInParent<PositionPlane>() != null)
                    {
                        var plane = b.GetComponentInParent<PositionPlane>();
                        Assert.AreEqual(.14f, new SerializedObject(plane).FindProperty("planeVisualOffset").floatValue);
                        Assert.AreEqual(.2f, new SerializedObject(a.GetComponentInParent<PositionPlane>()).FindProperty("planeVisualOffset").floatValue);
                        var pick = (BoxCollider)b;
                        var size = Vector3.Scale(pick.size, pick.transform.localScale);
                        Assert.That(size.x, Is.EqualTo(.20f).Within(.00001f));
                        Assert.That(size.z, Is.EqualTo(.20f).Within(.00001f));
                        Assert.AreEqual(Vector3.zero, pick.center);
                        var mesh = pick.GetComponent<MeshFilter>().sharedMesh;
                        Assert.That(mesh.bounds.size.x * pick.transform.localScale.x, Is.EqualTo(.18f).Within(.00001f));
                        Assert.That(mesh.bounds.size.z * pick.transform.localScale.z, Is.EqualTo(.18f).Within(.00001f));
                        Assert.Less(mesh.bounds.size.y, .00001f);
                        continue;
                    }
                    Assert.Less(Vector3.Distance(a.transform.position, b.transform.position), .00001f);
                    Assert.Less(Quaternion.Angle(a.transform.rotation, b.transform.rotation), .01f);
                    if (a is BoxCollider box)
                    {
                        var other = (BoxCollider)b;
                        Assert.Less(Vector3.Distance(Vector3.Scale(box.size, box.transform.lossyScale),
                            Vector3.Scale(other.size, other.transform.lossyScale)), .00001f);
                        Assert.Less(Vector3.Distance(box.transform.TransformPoint(box.center), other.transform.TransformPoint(other.center)), .00001f);
                    }
                    else if (a is MeshCollider mesh)
                    {
                        Assert.AreEqual(mesh.sharedMesh, ((MeshCollider)b).sharedMesh);
                        Assert.AreEqual(a.transform.lossyScale, b.transform.lossyScale);
                    }
                }
            }
            finally { Object.DestroyImmediate(classic); Object.DestroyImmediate(precision); }
        }

        [Test]
        public void PlaneBordersAreOptInAndMatchSerializedDefaultColors()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PrecisionTransformHandle.prefab");
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                var plane = renderer.GetComponentInParent<PositionPlane>(true);
                if (plane == null)
                {
                    Assert.AreEqual(0f, material.GetFloat("_PlaneBorderWidth"), material.name);
                    continue;
                }
                Assert.That(material.GetFloat("_PlaneBorderWidth"), Is.InRange(.02f, .06f));
                Assert.That(material.GetFloat("_PlaneBorderOpacity"), Is.GreaterThan(.9f));
                Assert.That(material.color.a, Is.InRange(.3f, .4f));
                Assert.AreEqual(material.color, new SerializedObject(plane).FindProperty("defaultColor").colorValue);
            }
            var settings = TransformHandleSettings.CreateDefault();
            try { Assert.IsFalse(settings.PreserveHandleColorOnHover); }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void OverlayQueuesSeparateEveryColoredPartAndBackingsCannotBePicked()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PrecisionTransformHandle.prefab");
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                Assert.AreEqual("Handles/Precision", renderer.sharedMaterial.shader.name, renderer.name);
                if (renderer.name == "Contrast backing")
                {
                    Assert.IsNull(renderer.GetComponent<Collider>());
                    Assert.AreEqual(3100, renderer.sharedMaterial.renderQueue);
                }
            }
            var materials = renderers.Select(r => r.sharedMaterial).Distinct().ToArray();
            Assert.AreEqual(14, materials.Length, "Nine axes, three planes, backing and center.");
            Assert.AreEqual(materials.Length, materials.Select(m => m.renderQueue).Distinct().Count());
            var position = prefab.GetComponentInChildren<PositionHandle>(true);
            Assert.AreEqual(3090, position.YPlane.GetComponentInChildren<MeshRenderer>(true).sharedMaterial.renderQueue, "XY plane");
            Assert.AreEqual(3091, position.ZPlane.GetComponentInChildren<MeshRenderer>(true).sharedMaterial.renderQueue, "YZ plane");
            Assert.AreEqual(3092, position.XPlane.GetComponentInChildren<MeshRenderer>(true).sharedMaterial.renderQueue, "XZ plane");
            var rotation = prefab.GetComponentsInChildren<RotationAxis>(true)[0];
            var arc = (Material)new SerializedObject(rotation).FindProperty("arcMaterial").objectReferenceValue;
            Assert.AreEqual(3093, arc.renderQueue);
        }
    }
}
