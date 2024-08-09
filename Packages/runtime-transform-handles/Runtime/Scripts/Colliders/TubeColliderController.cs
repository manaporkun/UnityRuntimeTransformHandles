using TransformHandles.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace TransformHandles
{
    public class TubeColliderController : MonoBehaviour
    {
        [FormerlySerializedAs("height")] [SerializeField] private float _height;
        [FormerlySerializedAs("sideCount")] [SerializeField] private int _sideCount;
        [FormerlySerializedAs("topRadius")] [SerializeField] private float _topRadius;
        [FormerlySerializedAs("bottomThickness")] [SerializeField] private float _bottomThickness;
        [FormerlySerializedAs("topThickness")] [SerializeField] private float _topThickness;
        [FormerlySerializedAs("bottomRadius")] [SerializeField] private float _bottomRadius;

        [FormerlySerializedAs("save")] [SerializeField] private bool _save;

        private MeshCollider _meshCollider;
        private MeshFilter _meshFilter;

        private void Awake()
        {
            //_meshCollider = GetComponent<MeshCollider>();
            _meshFilter = GetComponent<MeshFilter>();
        }

        private void Start()
        {
            UpdateCollider();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                UpdateCollider();
            }
        }

        private void UpdateCollider()
        {
            var newMesh = MeshUtils.CreateTube(_height, _sideCount, _bottomRadius, _bottomThickness, _topRadius, _topThickness);
			
            newMesh.name = "tube";
			
            _meshFilter.sharedMesh = newMesh;
            //_meshCollider.sharedMesh = newMesh;

            /*if (save)
            {
                AssetDatabase.CreateAsset(newMesh, "Assets/tube.asset");
            }*/
        }
    }
}