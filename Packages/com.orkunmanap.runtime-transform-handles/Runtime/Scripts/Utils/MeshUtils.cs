using UnityEngine;

namespace TransformHandles.Utils
{
    public class MeshUtils
    {
	    public static Mesh CreateArc(Vector3 center, Vector3 startPoint, Vector3 axis, float radius, float angle, int segmentCount)
		{
			var mesh = new Mesh();
			
			var vertices = new Vector3[segmentCount+2];

			var startVector = (startPoint - center).normalized * radius;
			for (var i = 0; i<=segmentCount; i++)
			{
				var rad = (float) i / segmentCount * angle;
				var v = Quaternion.AngleAxis(rad*180f/Mathf.PI, axis) * startVector;
				vertices[i] = v + center;
			}
			vertices[segmentCount+1] = center;
			
			var normals = new Vector3[vertices.Length];
			for( var n = 0; n < normals.Length; n++ )
				normals[n] = Vector3.up;

			var uvs = new Vector2[vertices.Length];
			for (var i = 0; i<=segmentCount; i++)
			{
				var rad = (float) i / segmentCount * angle;
				uvs[i] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
			}
			uvs[segmentCount + 1] = Vector2.one / 2f;
			
			var triangles = new int[ segmentCount * 3 ];
			for (var i = 0; i < segmentCount; i++)
			{
				var index = i * 3;
				triangles[index] = segmentCount+1;
				triangles[index+1] = i;
				triangles[index+2] = i + 1;
			}
			
			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;
 
			mesh.RecalculateBounds();
			mesh.Optimize();
			
			return mesh;
		}
		
		public static Mesh CreateArc(float radius, float angle, int segmentCount)
		{
			var mesh = new Mesh();
			
			var vertices = new Vector3[segmentCount+2];
			
			for (var i = 0; i<=segmentCount; i++)
			{
				var rad = (float) i / segmentCount * angle;
				vertices[i] = new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
			}
			vertices[segmentCount+1] = Vector3.zero;
			
			var normals = new Vector3[vertices.Length];
			for( var n = 0; n < normals.Length; n++ )
				normals[n] = Vector3.up;

			var uvs = new Vector2[vertices.Length];
			for (var i = 0; i<=segmentCount; i++)
			{
				var rad = (float) i / segmentCount * angle;
				uvs[i] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
			}
			uvs[segmentCount + 1] = Vector2.one / 2f;
			
			var triangles = new int[ segmentCount * 3 ];
			for (var i = 0; i < segmentCount; i++)
			{
				var index = i * 3;
				triangles[index] = segmentCount+1;
				triangles[index+1] = i;
				triangles[index+2] = i + 1;
			}
			
			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;
 
			mesh.RecalculateBounds();
			mesh.Optimize();
			
			return mesh;
		}
		
		public static Mesh CreateGrid(float width, float height, int segmentsX = 1, int segmentsY = 1)
		{
			var mesh = new Mesh();

			var resX = segmentsX + 1;
			var resZ = segmentsY + 1;
			
			var vertices = new Vector3[ resX * resZ ];
			for(var z = 0; z < resZ; z++)
			{
				var zPos = ((float)z / (resZ - 1) - .5f) * height;
				for(var x = 0; x < resX; x++)
				{
					var xPos = ((float)x / (resX - 1) - .5f) * width;
					vertices[ x + z * resX ] = new Vector3( xPos, 0f, zPos );
				}
			}

			
			var normals = new Vector3[ vertices.Length ];
			for( var n = 0; n < normals.Length; n++ )
				normals[n] = Vector3.up;

			
			var uvs = new Vector2[ vertices.Length ];
			for(var v = 0; v < resZ; v++)
			{
				for(var u = 0; u < resX; u++)
				{
					uvs[ u + v * resX ] = new Vector2( (float)u / (resX - 1), (float)v / (resZ - 1) );
				}
			}


			var faceCount = (resX - 1) * (resZ - 1);
			var triangles = new int[ faceCount * 6 ];
			var t = 0;
			for(var face = 0; face < faceCount; face++ )
			{
				// Retrieve lower left corner from face ind
				var i = face % (resX - 1) + (face / (resZ - 1) * resX);
 
				triangles[t++] = i + resX;
				triangles[t++] = i + 1;
				triangles[t++] = i;
 
				triangles[t++] = i + resX;	
				triangles[t++] = i + resX + 1;
				triangles[t++] = i + 1; 
			}

 
			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;
 
			mesh.RecalculateBounds();
			mesh.Optimize();

			return mesh;
		}
		
		public static Mesh CreateBox(float width, float height, float depth)
		{
			var mesh = new Mesh();

			var v0 = new Vector3(-depth * .5f, -width * .5f, height * .5f);
			var v1 = new Vector3(depth * .5f, -width * .5f, height * .5f);
			var v2 = new Vector3(depth * .5f, -width * .5f, -height * .5f);
			var v3 = new Vector3(-depth * .5f, -width * .5f, -height * .5f);

			var v4 = new Vector3(-depth * .5f, width * .5f, height * .5f);
			var v5 = new Vector3(depth * .5f, width * .5f, height * .5f);
			var v6 = new Vector3(depth * .5f, width * .5f, -height * .5f);
			var v7 = new Vector3(-depth * .5f, width * .5f, -height * .5f);

			var vertices = new[]
			{
				// Bottom
				v0, v1, v2, v3,

				// Left
				v7, v4, v0, v3,

				// Front
				v4, v5, v1, v0,

				// Back
				v6, v7, v3, v2,

				// Right
				v5, v6, v2, v1,

				// Top
				v7, v6, v5, v4
			};

			var up = Vector3.up;
			var down = Vector3.down;
			var front = Vector3.forward;
			var back = Vector3.back;
			var left = Vector3.left;
			var right = Vector3.right;

			var normals = new[]
			{
				down, down, down, down,

				left, left, left, left,

				front, front, front, front,

				back, back, back, back,

				right, right, right, right,

				up, up, up, up
			};

			var _00 = new Vector2(0f, 0f);
			var _10 = new Vector2(1f, 0f);
			var _01 = new Vector2(0f, 1f);
			var _11 = new Vector2(1f, 1f);

			var uvs = new[]
			{
				// Bottom
				_11, _01, _00, _10,

				// Left
				_11, _01, _00, _10,

				// Front
				_11, _01, _00, _10,

				// Back
				_11, _01, _00, _10,

				// Right
				_11, _01, _00, _10,

				// Top
				_11, _01, _00, _10,
			};

			var triangles = new[]
			{
				// Bottom
				3, 1, 0,
				3, 2, 1,

				// Left
				3 + 4 * 1, 1 + 4 * 1, 0 + 4 * 1,
				3 + 4 * 1, 2 + 4 * 1, 1 + 4 * 1,

				// Front
				3 + 4 * 2, 1 + 4 * 2, 0 + 4 * 2,
				3 + 4 * 2, 2 + 4 * 2, 1 + 4 * 2,

				// Back
				3 + 4 * 3, 1 + 4 * 3, 0 + 4 * 3,
				3 + 4 * 3, 2 + 4 * 3, 1 + 4 * 3,

				// Right
				3 + 4 * 4, 1 + 4 * 4, 0 + 4 * 4,
				3 + 4 * 4, 2 + 4 * 4, 1 + 4 * 4,

				// Top
				3 + 4 * 5, 1 + 4 * 5, 0 + 4 * 5,
				3 + 4 * 5, 2 + 4 * 5, 1 + 4 * 5,
			};

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			mesh.RecalculateBounds();
			mesh.Optimize();

			return mesh;
		}

		public static Mesh CreateCone(float height, float bottomRadius, float topRadius, int sideCount,
			int heightSegmentCount)
		{
			var mesh = new Mesh();

			var vertexCapCount = sideCount + 1;

			// bottom + top + sides
			var vertices =
				new Vector3[vertexCapCount + vertexCapCount + sideCount * heightSegmentCount * 2 + 2];
			var vert = 0;
			var _2pi = Mathf.PI * 2f;

			// Bottom cap
			vertices[vert++] = new Vector3(0f, 0f, 0f);
			while (vert <= sideCount)
			{
				var rad = (float) vert / sideCount * _2pi;
				vertices[vert] = new Vector3(Mathf.Cos(rad) * bottomRadius, 0f, Mathf.Sin(rad) * bottomRadius);
				vert++;
			}

			// Top cap
			vertices[vert++] = new Vector3(0f, height, 0f);
			while (vert <= sideCount * 2 + 1)
			{
				var rad = (float) (vert - sideCount - 1) / sideCount * _2pi;
				vertices[vert] = new Vector3(Mathf.Cos(rad) * topRadius, height, Mathf.Sin(rad) * topRadius);
				vert++;
			}

			// Sides
			var v = 0;
			while (vert <= vertices.Length - 4)
			{
				var rad = (float) v / sideCount * _2pi;
				vertices[vert] = new Vector3(Mathf.Cos(rad) * topRadius, height, Mathf.Sin(rad) * topRadius);
				vertices[vert + 1] = new Vector3(Mathf.Cos(rad) * bottomRadius, 0, Mathf.Sin(rad) * bottomRadius);
				vert += 2;
				v++;
			}

			vertices[vert] = vertices[sideCount * 2 + 2];
			vertices[vert + 1] = vertices[sideCount * 2 + 3];


			// bottom + top + sides
			var normals = new Vector3[vertices.Length];
			vert = 0;

			// Bottom cap
			while (vert <= sideCount)
			{
				normals[vert++] = Vector3.down;
			}

			// Top cap
			while (vert <= sideCount * 2 + 1)
			{
				normals[vert++] = Vector3.up;
			}

			// Sides
			v = 0;
			while (vert <= vertices.Length - 4)
			{
				var rad = (float) v / sideCount * _2pi;
				var cos = Mathf.Cos(rad);
				var sin = Mathf.Sin(rad);

				normals[vert] = new Vector3(cos, 0f, sin);
				normals[vert + 1] = normals[vert];

				vert += 2;
				v++;
			}

			normals[vert] = normals[sideCount * 2 + 2];
			normals[vert + 1] = normals[sideCount * 2 + 3];


			var uvs = new Vector2[vertices.Length];

			// Bottom cap
			var u = 0;
			uvs[u++] = new Vector2(0.5f, 0.5f);
			while (u <= sideCount)
			{
				var rad = (float) u / sideCount * _2pi;
				uvs[u] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
				u++;
			}

			// Top cap
			uvs[u++] = new Vector2(0.5f, 0.5f);
			while (u <= sideCount * 2 + 1)
			{
				var rad = (float) u / sideCount * _2pi;
				uvs[u] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
				u++;
			}

			// Sides
			var uSides = 0;
			while (u <= uvs.Length - 4)
			{
				var t = (float) uSides / sideCount;
				uvs[u] = new Vector3(t, 1f);
				uvs[u + 1] = new Vector3(t, 0f);
				u += 2;
				uSides++;
			}

			uvs[u] = new Vector2(1f, 1f);
			uvs[u + 1] = new Vector2(1f, 0f);

			var triangleCount = sideCount + sideCount + sideCount * 2;
			var triangles = new int[triangleCount * 3 + 3];

			// Bottom cap
			var tri = 0;
			var i = 0;
			while (tri < sideCount - 1)
			{
				triangles[i] = 0;
				triangles[i + 1] = tri + 1;
				triangles[i + 2] = tri + 2;
				tri++;
				i += 3;
			}

			triangles[i] = 0;
			triangles[i + 1] = tri + 1;
			triangles[i + 2] = 1;
			tri++;
			i += 3;

			// Top cap
			while (tri < sideCount * 2)
			{
				triangles[i] = tri + 2;
				triangles[i + 1] = tri + 1;
				triangles[i + 2] = vertexCapCount;
				tri++;
				i += 3;
			}

			triangles[i] = vertexCapCount + 1;
			triangles[i + 1] = tri + 1;
			triangles[i + 2] = vertexCapCount;
			tri++;
			i += 3;
			tri++;

			// Sides
			while (tri <= triangleCount)
			{
				triangles[i] = tri + 2;
				triangles[i + 1] = tri + 1;
				triangles[i + 2] = tri + 0;
				tri++;
				i += 3;

				triangles[i] = tri + 1;
				triangles[i + 1] = tri + 2;
				triangles[i + 2] = tri + 0;
				tri++;
				i += 3;
			}


			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			mesh.RecalculateBounds();
			mesh.Optimize();

			return mesh;
		}

		public static Mesh CreateTube(float height, int sideCount, float bottomRadius, float bottomThickness,
			float topRadius, float topThickness)
		{
			var mesh = new Mesh();

			var vertexCapCount = sideCount * 2 + 2;
			var vertexSideCount = sideCount * 2 + 2;


			// bottom + top + sides
			var vertices = new Vector3[vertexCapCount * 2 + vertexSideCount * 2];
			var vert = 0;
			var _2pi = Mathf.PI * 2f;

			// Bottom cap
			var sideCounter = 0;
			while (vert < vertexCapCount)
			{
				sideCounter = sideCounter == sideCount ? 0 : sideCounter;

				var r1 = (float) (sideCounter++) / sideCount * _2pi;
				var cos = Mathf.Cos(r1);
				var sin = Mathf.Sin(r1);
				vertices[vert] = new Vector3(cos * (bottomRadius - bottomThickness * .5f), 0f,
					sin * (bottomRadius - bottomThickness * .5f));
				vertices[vert + 1] = new Vector3(cos * (bottomRadius + bottomThickness * .5f), 0f,
					sin * (bottomRadius + bottomThickness * .5f));
				vert += 2;
			}

			// Top cap
			sideCounter = 0;
			while (vert < vertexCapCount * 2)
			{
				sideCounter = sideCounter == sideCount ? 0 : sideCounter;

				var r1 = (float) (sideCounter++) / sideCount * _2pi;
				var cos = Mathf.Cos(r1);
				var sin = Mathf.Sin(r1);
				vertices[vert] = new Vector3(cos * (topRadius - topThickness * .5f), height,
					sin * (topRadius - topThickness * .5f));
				vertices[vert + 1] = new Vector3(cos * (topRadius + topThickness * .5f), height,
					sin * (topRadius + topThickness * .5f));
				vert += 2;
			}

			// Sides (out)
			sideCounter = 0;
			while (vert < vertexCapCount * 2 + vertexSideCount)
			{
				sideCounter = sideCounter == sideCount ? 0 : sideCounter;

				var r1 = (float) (sideCounter++) / sideCount * _2pi;
				var cos = Mathf.Cos(r1);
				var sin = Mathf.Sin(r1);

				vertices[vert] = new Vector3(cos * (topRadius + topThickness * .5f), height,
					sin * (topRadius + topThickness * .5f));
				vertices[vert + 1] = new Vector3(cos * (bottomRadius + bottomThickness * .5f), 0,
					sin * (bottomRadius + bottomThickness * .5f));
				vert += 2;
			}

			// Sides (in)
			sideCounter = 0;
			while (vert < vertices.Length)
			{
				sideCounter = sideCounter == sideCount ? 0 : sideCounter;

				var r1 = (float) (sideCounter++) / sideCount * _2pi;
				var cos = Mathf.Cos(r1);
				var sin = Mathf.Sin(r1);

				vertices[vert] = new Vector3(cos * (topRadius - topThickness * .5f), height,
					sin * (topRadius - topThickness * .5f));
				vertices[vert + 1] = new Vector3(cos * (bottomRadius - bottomThickness * .5f), 0,
					sin * (bottomRadius - bottomThickness * .5f));
				vert += 2;
			}


			// bottom + top + sides
			var normals = new Vector3[vertices.Length];
			vert = 0;

			// Bottom cap
			while (vert < vertexCapCount)
			{
				normals[vert++] = Vector3.down;
			}

			// Top cap
			while (vert < vertexCapCount * 2)
			{
				normals[vert++] = Vector3.up;
			}

			// Sides (out)
			sideCounter = 0;
			while (vert < vertexCapCount * 2 + vertexSideCount)
			{
				sideCounter = sideCounter == sideCount ? 0 : sideCounter;

				var r1 = (float) (sideCounter++) / sideCount * _2pi;

				normals[vert] = new Vector3(Mathf.Cos(r1), 0f, Mathf.Sin(r1));
				normals[vert + 1] = normals[vert];
				vert += 2;
			}

			// Sides (in)
			sideCounter = 0;
			while (vert < vertices.Length)
			{
				sideCounter = sideCounter == sideCount ? 0 : sideCounter;

				var r1 = (float) (sideCounter++) / sideCount * _2pi;

				normals[vert] = -(new Vector3(Mathf.Cos(r1), 0f, Mathf.Sin(r1)));
				normals[vert + 1] = normals[vert];
				vert += 2;
			}

			var uvs = new Vector2[vertices.Length];

			vert = 0;
			// Bottom cap
			sideCounter = 0;
			while (vert < vertexCapCount)
			{
				var t = (float) (sideCounter++) / sideCount;
				uvs[vert++] = new Vector2(0f, t);
				uvs[vert++] = new Vector2(1f, t);
			}

			// Top cap
			sideCounter = 0;
			while (vert < vertexCapCount * 2)
			{
				var t = (float) (sideCounter++) / sideCount;
				uvs[vert++] = new Vector2(0f, t);
				uvs[vert++] = new Vector2(1f, t);
			}

			// Sides (out)
			sideCounter = 0;
			while (vert < vertexCapCount * 2 + vertexSideCount)
			{
				var t = (float) (sideCounter++) / sideCount;
				uvs[vert++] = new Vector2(t, 0f);
				uvs[vert++] = new Vector2(t, 1f);
			}

			// Sides (in)
			sideCounter = 0;
			while (vert < vertices.Length)
			{
				var t = (float) (sideCounter++) / sideCount;
				uvs[vert++] = new Vector2(t, 0f);
				uvs[vert++] = new Vector2(t, 1f);
			}

			var faceCount = sideCount * 4;
			var triangleCount = faceCount * 2;
			var indexCount = triangleCount * 3;
			var triangles = new int[indexCount];

			// Bottom cap
			var i = 0;
			sideCounter = 0;
			while (sideCounter < sideCount)
			{
				var current = sideCounter * 2;
				var next = sideCounter * 2 + 2;

				triangles[i++] = next + 1;
				triangles[i++] = next;
				triangles[i++] = current;

				triangles[i++] = current + 1;
				triangles[i++] = next + 1;
				triangles[i++] = current;

				sideCounter++;
			}

			// Top cap
			while (sideCounter < sideCount * 2)
			{
				var current = sideCounter * 2 + 2;
				var next = sideCounter * 2 + 4;

				triangles[i++] = current;
				triangles[i++] = next;
				triangles[i++] = next + 1;

				triangles[i++] = current;
				triangles[i++] = next + 1;
				triangles[i++] = current + 1;

				sideCounter++;
			}

			// Sides (out)
			while (sideCounter < sideCount * 3)
			{
				var current = sideCounter * 2 + 4;
				var next = sideCounter * 2 + 6;

				triangles[i++] = current;
				triangles[i++] = next;
				triangles[i++] = next + 1;

				triangles[i++] = current;
				triangles[i++] = next + 1;
				triangles[i++] = current + 1;

				sideCounter++;
			}


			// Sides (in)
			while (sideCounter < sideCount * 4)
			{
				var current = sideCounter * 2 + 6;
				var next = sideCounter * 2 + 8;

				triangles[i++] = next + 1;
				triangles[i++] = next;
				triangles[i++] = current;

				triangles[i++] = current + 1;
				triangles[i++] = next + 1;
				triangles[i++] = current;

				sideCounter++;
			}

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			mesh.RecalculateBounds();
			mesh.Optimize();

			return mesh;
		}

		public static Mesh CreateTorus(float radius, float thickness, int radiusSegmentCount, int sideCount)
		{
			var mesh = new Mesh();


			var vertices = new Vector3[(radiusSegmentCount + 1) * (sideCount + 1)];
			var _2pi = Mathf.PI * 2f;
			for (var seg = 0; seg <= radiusSegmentCount; seg++)
			{
				var currSeg = seg == radiusSegmentCount ? 0 : seg;

				var t1 = (float) currSeg / radiusSegmentCount * _2pi;
				var r1 = new Vector3(Mathf.Cos(t1) * radius, 0f, Mathf.Sin(t1) * radius);

				for (var side = 0; side <= sideCount; side++)
				{
					var currSide = side == sideCount ? 0 : side;

					Vector3.Cross(r1, Vector3.up);
					var t2 = (float) currSide / sideCount * _2pi;
					var r2 = Quaternion.AngleAxis(-t1 * Mathf.Rad2Deg, Vector3.up) *
					         new Vector3(Mathf.Sin(t2) * thickness, Mathf.Cos(t2) * thickness);

					vertices[side + seg * (sideCount + 1)] = r1 + r2;
				}
			}


			var normals = new Vector3[vertices.Length];
			for (var seg = 0; seg <= radiusSegmentCount; seg++)
			{
				var currSeg = seg == radiusSegmentCount ? 0 : seg;

				var t1 = (float) currSeg / radiusSegmentCount * _2pi;
				var r1 = new Vector3(Mathf.Cos(t1) * radius, 0f, Mathf.Sin(t1) * radius);

				for (var side = 0; side <= sideCount; side++)
				{
					normals[side + seg * (sideCount + 1)] =
						(vertices[side + seg * (sideCount + 1)] - r1).normalized;
				}
			}


			var uvs = new Vector2[vertices.Length];
			for (var seg = 0; seg <= radiusSegmentCount; seg++)
			for (var side = 0; side <= sideCount; side++)
				uvs[side + seg * (sideCount + 1)] =
					new Vector2((float) seg / radiusSegmentCount, (float) side / sideCount);


			var faceCount = vertices.Length;
			var triangleCount = faceCount * 2;
			var indexCount = triangleCount * 3;
			var triangles = new int[indexCount];

			var i = 0;
			for (var seg = 0; seg <= radiusSegmentCount; seg++)
			{
				for (var side = 0; side <= sideCount - 1; side++)
				{
					var current = side + seg * (sideCount + 1);
					var next = side + (seg < (radiusSegmentCount) ? (seg + 1) * (sideCount + 1) : 0);

					if (i >= triangles.Length - 6) continue;
					triangles[i++] = current;
					triangles[i++] = next;
					triangles[i++] = next + 1;

					triangles[i++] = current;
					triangles[i++] = next + 1;
					triangles[i++] = current + 1;
				}
			}

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			mesh.RecalculateBounds();
			mesh.Optimize();

			return mesh;
		}
		
		public static Mesh CreateSphere(float radius, int longitudeCount, int latitudeCount)
		{
			var mesh = new Mesh();
			mesh.Clear();
			
			var vertices = new Vector3[(longitudeCount+1) * latitudeCount + 2];
			var pi = Mathf.PI;
			var _2pi = pi * 2f;
			 
			vertices[0] = Vector3.up * radius;
			for( var lat = 0; lat < latitudeCount; lat++ )
			{
				var a1 = pi * (lat+1) / (latitudeCount+1);
				var sin1 = Mathf.Sin(a1);
				var cos1 = Mathf.Cos(a1);
			 
				for( var lon = 0; lon <= longitudeCount; lon++ )
				{
					var a2 = _2pi * (lon == longitudeCount ? 0 : lon) / longitudeCount;
					var sin2 = Mathf.Sin(a2);
					var cos2 = Mathf.Cos(a2);
			 
					vertices[ lon + lat * (longitudeCount + 1) + 1] = new Vector3( sin1 * cos2, cos1, sin1 * sin2 ) * radius;
				}
			}
			vertices[^1] = Vector3.up * -radius;


			var normals = new Vector3[vertices.Length];
			for( var n = 0; n < vertices.Length; n++ )
				normals[n] = vertices[n].normalized;

			
			var uvs = new Vector2[vertices.Length];
			uvs[0] = Vector2.up;
			uvs[^1] = Vector2.zero;
			for( var lat = 0; lat < latitudeCount; lat++ )
				for( var lon = 0; lon <= longitudeCount; lon++ )
					uvs[lon + lat * (longitudeCount + 1) + 1] = new Vector2( (float)lon / longitudeCount, 1f - (float)(lat+1) / (latitudeCount+1) );
			
			var faceCount = vertices.Length;
			var triangleCount = faceCount * 2;
			var indexCount = triangleCount * 3;
			var triangles = new int[ indexCount ];
			 
			//Top Cap
			var i = 0;
			for( var lon = 0; lon < longitudeCount; lon++ )
			{
				triangles[i++] = lon+2;
				triangles[i++] = lon+1;
				triangles[i++] = 0;
			}
			 
			//Middle
			for( var lat = 0; lat < latitudeCount - 1; lat++ )
			{
				for( var lon = 0; lon < longitudeCount; lon++ )
				{
					var current = lon + lat * (longitudeCount + 1) + 1;
					var next = current + longitudeCount + 1;
			 
					triangles[i++] = current;
					triangles[i++] = current + 1;
					triangles[i++] = next + 1;
			 
					triangles[i++] = current;
					triangles[i++] = next + 1;
					triangles[i++] = next;
				}
			}
			 
			//Bottom Cap
			for( var lon = 0; lon < longitudeCount; lon++ )
			{
				triangles[i++] = vertices.Length - 1;
				triangles[i++] = vertices.Length - (lon+2) - 1;
				triangles[i++] = vertices.Length - (lon+1) - 1;
			}

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;
			 
			mesh.RecalculateBounds();
			mesh.Optimize();

			return mesh;
		}
    }
}