using TransformHandles.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace TransformHandles
{
	public class ConeColliderController : MonoBehaviour
	{
		[FormerlySerializedAs("sideCount")] [SerializeField] private int _sideCount = 15;
		[FormerlySerializedAs("topRadius")] [SerializeField] private float _topRadius = 0.02f;
		[FormerlySerializedAs("heightSegmentCount")] [SerializeField] private int _heightSegmentCount = 1;
		
		[FormerlySerializedAs("colliderTransform")] [SerializeField] private Transform _colliderTransform;

		[FormerlySerializedAs("height")] [SerializeField] private float _height;
		[FormerlySerializedAs("bottomRadius")] [SerializeField] private float _bottomRadius;

		[FormerlySerializedAs("save")] [SerializeField] private bool _save;

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
			var newMesh = MeshUtils.CreateCone(
				_height, 
				_bottomRadius, 
				_topRadius, _sideCount, _heightSegmentCount);
			
			newMesh.name = "cone";
			
			_meshFilter.sharedMesh = newMesh;
			_meshCollider.sharedMesh = newMesh;
			
			/*if (save)
			{
				AssetDatabase.CreateAsset(newMesh, "Assets/cone.asset");
			}*/
		}
	}
}