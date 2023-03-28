using TransformHandles.Utils;
using UnityEditor;
using UnityEngine;

namespace TransformHandles
{
    public class TubeColliderController : MonoBehaviour
    {
        [SerializeField] private float height;
        [SerializeField] private int sideCount;
        [SerializeField] private float topRadius;
        [SerializeField] private float bottomThickness;
        [SerializeField] private float topThickness;
        [SerializeField] private float bottomRadius;

        [SerializeField] private bool save;

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
            var newMesh = MeshUtils.CreateTube(height, sideCount, bottomRadius, bottomThickness, topRadius, topThickness);
			
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