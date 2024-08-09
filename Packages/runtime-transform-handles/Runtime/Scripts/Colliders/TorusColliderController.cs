using TransformHandles.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace TransformHandles
{
	public class TorusColliderController : MonoBehaviour
	{
		[FormerlySerializedAs("segmentCount")] [SerializeField] private int _segmentCount = 32;
		[FormerlySerializedAs("sideCount")] [SerializeField] private int _sideCount = 15;
		[FormerlySerializedAs("radius")] [SerializeField] private float _radius;
		[FormerlySerializedAs("thickness")] [SerializeField] private float _thickness;

		[FormerlySerializedAs("colliderTransform")] [SerializeField] private Transform _colliderTransform;
		
		private MeshCollider _meshCollider;
		private MeshFilter _meshFilter;

		private void Awake()
		{
			_meshCollider = _colliderTransform.GetComponent<MeshCollider>();
			_meshFilter = _colliderTransform.GetComponent<MeshFilter>();
		}

		private void Start()
		{
			UpdateCollider();
		}
		
		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.K))
			{
				UpdateCollider();
			}
		}

		private void UpdateCollider()
		{
			var newMesh = MeshUtils.CreateTorus(_radius, _thickness, _segmentCount, _sideCount);
			newMesh.name = "torus";
			
			_meshFilter.sharedMesh = newMesh;
			_meshCollider.sharedMesh = newMesh;
			
			/*
			AssetDatabase.CreateAsset(newMesh, "Assets/torus.asset");
			*/
		}
	}
}