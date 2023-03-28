using TransformHandles.Utils;
using UnityEditor;
using UnityEngine;

namespace TransformHandles
{
	public class ConeColliderController : MonoBehaviour
	{
		[SerializeField] private int sideCount = 15;
		[SerializeField] private float topRadius = 0.02f;
		[SerializeField] private int heightSegmentCount = 1;
		
		[SerializeField] private Transform colliderTransform;

		[SerializeField] private float height;
		[SerializeField] private float bottomRadius;

		[SerializeField] private bool save;

		private MeshCollider _meshCollider;
		private MeshFilter _meshFilter;

		private void Awake()
		{
			_meshCollider = colliderTransform.GetComponent<MeshCollider>();
			_meshFilter = colliderTransform.GetComponent<MeshFilter>();
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
				height, 
				bottomRadius, 
				topRadius, sideCount, heightSegmentCount);
			
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