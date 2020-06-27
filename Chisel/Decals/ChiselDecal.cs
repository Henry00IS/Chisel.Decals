using Chisel.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.UIElements;

namespace AeternumGames.Chisel.Decals
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ChiselDecal : MonoBehaviour
    {
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        [SerializeField]
        private int lastInstanceID;

        private void OnEnable()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            // ensure a mesh is assigned to the mesh filter.
            if (meshFilter.sharedMesh == null)
            {
                ResetDecalMesh();
            }
            else
            {
                // ensure that if duplicated we have a unique mesh to work with.
                if (lastInstanceID != GetInstanceID())
                {
                    ResetDecalMesh();
                }
            }

            // remember the last instance id.
            lastInstanceID = GetInstanceID();
        }

        private void Update()
        {
            // find all mesh colliders that intersect with the decal projector box.
            List<MeshCollider> meshColliders = new List<MeshCollider>();
            FindMeshColliders(meshColliders);

            Mesh mesh = meshFilter.sharedMesh;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            Bounds projectorBounds = GetBounds();
            projectorBounds.center = transform.position - projectorBounds.center;

            foreach (var meshCollider in meshColliders)
            {
                Mesh colliderMesh = meshCollider.sharedMesh;
                Vector3[] colliderVertices = colliderMesh.vertices;
                int[] colliderTriangles = colliderMesh.GetTriangles(0);

                for (int i = 0; i < colliderTriangles.Length; i += 3)
                {
                    // fetch a triangle from the collider.
                    Vector3 colliderVertex1 = colliderVertices[colliderTriangles[i]];
                    Vector3 colliderVertex2 = colliderVertices[colliderTriangles[i + 1]];
                    Vector3 colliderVertex3 = colliderVertices[colliderTriangles[i + 2]];

                    // apply any modification from their transform.
                    colliderVertex1 = meshCollider.transform.TransformPoint(colliderVertex1);
                    colliderVertex2 = meshCollider.transform.TransformPoint(colliderVertex2);
                    colliderVertex3 = meshCollider.transform.TransformPoint(colliderVertex3);

                    // now the mesh vertices are in world space 1:1 to how they appear in the scene.

                    // if the triangle bounds are completely outside of the projector bounds we discard it.
                    Bounds triangleBounds = new Bounds(colliderVertex1, Vector3.zero);
                    triangleBounds.Encapsulate(colliderVertex2);
                    triangleBounds.Encapsulate(colliderVertex3);
                    if (!projectorBounds.Intersects(triangleBounds)) continue;

                    Vector3 r = Vector3.right;
                    Vector3 f = Vector3.forward;
                    Vector3 u = Vector3.up;

                    r = transform.TransformVector(r * 0.5f);
                    f = transform.TransformVector(f * 0.5f);
                    u = transform.TransformVector(u * 0.5f);

                    Plane clipRL = new Plane(-r, transform.position + r);
                    Plane clipLR = new Plane(r, transform.position - r);
                    Plane clipBBT = new Plane(-f, transform.position + f);
                    Plane clipFBT = new Plane(f, transform.position - f);
                    Plane clipTBF = new Plane(-u, transform.position + u + f);
                    Plane clipBBF = new Plane(u, transform.position - u + f);
                    List<Vector3> poly = ClipPolygon(new List<Vector3>() { colliderVertex1, colliderVertex2, colliderVertex3 }, new List<Plane>() { clipRL, clipLR, clipBBT, clipFBT, clipTBF, clipBBF });

                    if (poly.Count >= 3)
                    {
                        foreach (var triangle in Triangulate(poly.ToArray()))
                        {
                            colliderVertex1 = triangle[0];
                            colliderVertex2 = triangle[1];
                            colliderVertex3 = triangle[2];

                            // create a horizontal and vertical plane through the projector box.
                            // the distance of the vertices to the planes are used to calculate UV coordinates.
                            Plane hplane = new Plane(u, transform.position);
                            Plane vplane = new Plane(r, transform.position);
                            Vector2 uv1 = new Vector2(vplane.GetDistanceToPoint(colliderVertex1) - 0.5f, hplane.GetDistanceToPoint(colliderVertex1) - 0.5f);
                            Vector2 uv2 = new Vector2(vplane.GetDistanceToPoint(colliderVertex2) - 0.5f, hplane.GetDistanceToPoint(colliderVertex2) - 0.5f);
                            Vector2 uv3 = new Vector2(vplane.GetDistanceToPoint(colliderVertex3) - 0.5f, hplane.GetDistanceToPoint(colliderVertex3) - 0.5f);

                            // undo our transformation so that the mesh looks correct in the scene.
                            colliderVertex1 = transform.InverseTransformPoint(colliderVertex1);
                            colliderVertex2 = transform.InverseTransformPoint(colliderVertex2);
                            colliderVertex3 = transform.InverseTransformPoint(colliderVertex3);

                            // ADD
                            Plane plane = new Plane(colliderVertex1, colliderVertex2, colliderVertex3);
                            colliderVertex1 += plane.normal * 0.002f;
                            colliderVertex2 += plane.normal * 0.002f;
                            colliderVertex3 += plane.normal * 0.002f;
                            vertices.Add(colliderVertex1); triangles.Add(vertices.Count - 1); uvs.Add(uv1);
                            vertices.Add(colliderVertex2); triangles.Add(vertices.Count - 1); uvs.Add(uv2);
                            vertices.Add(colliderVertex3); triangles.Add(vertices.Count - 1); uvs.Add(uv3);
                            // ADD
                        }
                    }
                }
            }

            if (vertices.Count > 0)
            {
                mesh.Clear();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.SetUVs(0, uvs);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
            }
            else
            {
                mesh.Clear();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.white;

            Bounds projectorBounds = GetBounds();
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position - projectorBounds.center, projectorBounds.size);

            Gizmos.DrawWireMesh(meshFilter.sharedMesh);
        }

        /// <summary>
        /// Resets the decal mesh and assigns it to the mesh filter.
        /// </summary>
        private void ResetDecalMesh()
        {
            Mesh decalMesh = new Mesh();
            decalMesh.name = "Chisel Decal";
            decalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = decalMesh;
        }

        /// <summary>
        /// Finds all mesh colliders that intersect with the decal projector box.
        /// </summary>
        /// <param name="results">The list that will contain all of the results.</param>
        private void FindMeshColliders(List<MeshCollider> results)
        {
            Collider[] colliders = Physics.OverlapBox(transform.position, transform.lossyScale * 0.5f, Quaternion.LookRotation(transform.forward));
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider.GetType() == typeof(MeshCollider))
                    results.Add((MeshCollider)collider);
            }
        }

        private static List<Vector3[]> Triangulate(Vector3[] polygon)
        {
            int triangleCount = polygon.Length - 2;
            List<Vector3[]> triangles = new List<Vector3[]>(triangleCount);

            // Calculate triangulation
            for (int j = 0; j < triangleCount; j++)
            {
                triangles.Add(new Vector3[] {
                    polygon[0],
                    polygon[j+1],
                    polygon[j+2]
                });
            }

            return triangles;
        }

        private Bounds GetBounds()
        {
            Bounds projectorBounds = new Bounds();
            {
                Vector3 r = Vector3.right;
                Vector3 f = Vector3.forward;
                Vector3 u = Vector3.up;

                r = transform.TransformVector(r * 0.5f);
                f = transform.TransformVector(f * 0.5f);
                u = transform.TransformVector(u * 0.5f);

                projectorBounds.Encapsulate(f + r + u);
                projectorBounds.Encapsulate(f - r + u);
                projectorBounds.Encapsulate(f + r - u);
                projectorBounds.Encapsulate(f - r - u);
                projectorBounds.Encapsulate(-f + r + u);
                projectorBounds.Encapsulate(-f - r + u);
                projectorBounds.Encapsulate(-f + r - u);
                projectorBounds.Encapsulate(-f - r - u);
            }
            return projectorBounds;
        }

        private void DrawPlane(Vector3 position, Vector3 normal)
        {
            Vector3 v3;

            if (normal.normalized != Vector3.forward)
                v3 = Vector3.Cross(normal, Vector3.forward).normalized * normal.magnitude;
            else
                v3 = Vector3.Cross(normal, Vector3.up).normalized * normal.magnitude; ;

            var corner0 = position + v3;
            var corner2 = position - v3;
            var q = Quaternion.AngleAxis(90.0f, normal);
            v3 = q * v3;
            var corner1 = position + v3;
            var corner3 = position - v3;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(corner0, corner2);
            Gizmos.DrawLine(corner1, corner3);
            Gizmos.DrawLine(corner0, corner1);
            Gizmos.DrawLine(corner1, corner2);
            Gizmos.DrawLine(corner2, corner3);
            Gizmos.DrawLine(corner3, corner0);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(position, normal);
        }

        public static List<Vector3> ClipPolygon(List<Vector3> poly_1, List<Plane> clippingPlanes)
        {
            //Clone the vertices because we will remove vertices from this list
            List<Vector3> vertices = new List<Vector3>(poly_1);

            //Save the new vertices temporarily in this list before transfering them to vertices
            List<Vector3> vertices_tmp = new List<Vector3>();

            //Clip the polygon
            for (int i = 0; i < clippingPlanes.Count; i++)
            {
                Plane plane = clippingPlanes[i];

                for (int j = 0; j < vertices.Count; j++)
                {
                    int jPlusOne = ClampListIndex(j + 1, vertices.Count);

                    Vector3 v1 = vertices[j];
                    Vector3 v2 = vertices[jPlusOne];

                    //Calculate the distance to the plane from each vertex
                    //This is how we will know if they are inside or outside
                    //If they are inside, the distance is positive, which is why the planes normals have to be oriented to the inside
                    float dist_to_v1 = DistanceFromPointToPlane(plane.normal, plane.ClosestPointOnPlane(Vector3.zero), v1);
                    float dist_to_v2 = DistanceFromPointToPlane(plane.normal, plane.ClosestPointOnPlane(Vector3.zero), v2);

                    //Case 1. Both are outside (= to the right), do nothing

                    //Case 2. Both are inside (= to the left), save v2
                    if (dist_to_v1 > 0f && dist_to_v2 > 0f)
                    {
                        vertices_tmp.Add(v2);
                    }
                    //Case 3. Outside -> Inside, save intersection point and v2
                    else if (dist_to_v1 < 0f && dist_to_v2 > 0f)
                    {
                        Vector3 rayDir = (v2 - v1).normalized;

                        Vector3 intersectionPoint = GetRayPlaneIntersectionCoordinate(plane.ClosestPointOnPlane(Vector3.zero), plane.normal, v1, rayDir);

                        vertices_tmp.Add(intersectionPoint);

                        vertices_tmp.Add(v2);
                    }
                    //Case 4. Inside -> Outside, save intersection point
                    else if (dist_to_v1 > 0f && dist_to_v2 < 0f)
                    {
                        Vector3 rayDir = (v2 - v1).normalized;

                        Vector3 intersectionPoint = GetRayPlaneIntersectionCoordinate(plane.ClosestPointOnPlane(Vector3.zero), plane.normal, v1, rayDir);

                        vertices_tmp.Add(intersectionPoint);
                    }
                }

                //Add the new vertices to the list of vertices
                vertices.Clear();

                vertices.AddRange(vertices_tmp);

                vertices_tmp.Clear();
            }

            return vertices;
        }

        //Get the coordinate if we know a ray-plane is intersecting
        public static Vector3 GetRayPlaneIntersectionCoordinate(Vector3 planePos, Vector3 planeNormal, Vector3 rayStart, Vector3 rayDir)
        {
            float denominator = Vector3.Dot(-planeNormal, rayDir);

            Vector3 vecBetween = planePos - rayStart;

            float t = Vector3.Dot(vecBetween, -planeNormal) / denominator;

            Vector3 intersectionPoint = rayStart + rayDir * t;

            return intersectionPoint;
        }

        public static int ClampListIndex(int index, int listSize)
        {
            index = ((index % listSize) + listSize) % listSize;

            return index;
        }

        public static float DistanceFromPointToPlane(Vector3 planeNormal, Vector3 planePos, Vector3 pointPos)
        {
            //Positive distance denotes that the point p is on the front side of the plane
            //Negative means it's on the back side
            float distance = Vector3.Dot(planeNormal, pointPos - planePos);

            return distance;
        }

        private static Vector3 MultiplyVector3(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }
    }
}