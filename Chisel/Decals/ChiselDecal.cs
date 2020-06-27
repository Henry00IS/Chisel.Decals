using System.Collections.Generic;
using UnityEngine;

namespace AeternumGames.Chisel.Decals
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ChiselDecal : MonoBehaviour
    {
        private MeshFilter meshFilter;

        [SerializeField] private Vector3 lastWorldPosition;
        [SerializeField] private Quaternion lastWorldRotation;
        [SerializeField] private Vector3 lastWorldScale;

        public Vector2 uvTiling = new Vector2(1.0f, 1.0f);
        public Vector2 uvOffset = new Vector2(0.0f, 0.0f);

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshFilter.hideFlags = HideFlags.NotEditable;

            // ensure a mesh is assigned to the mesh filter.
            if (meshFilter.sharedMesh == null)
            {
                ResetDecalMesh();
            }
        }

        private void Update()
        {
            // only rebuild the decal mesh if we were modified.
            if (!IsDirty()) return;
            // update the flags we need to tell if we have to rebuild the decal mesh.
            UpdateDirtyFlags();
            // rebuild the decal mesh.
            Rebuild();
        }

        public void Rebuild()
        {
            // always create a new mesh as duplication will copy our shared mesh.
            // would have preferred mesh.Clear() but there's just no clean way to do it atm.
            Mesh mesh = ResetDecalMesh();

            // update the flags we need to tell if we have to rebuild the decal mesh.
            UpdateDirtyFlags();

            // find all mesh colliders that intersect with the decal projector box.
            List<MeshCollider> meshColliders = new List<MeshCollider>();
            FindMeshColliders(meshColliders);

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

                    Vector3 r = transform.TransformVector(Vector3.right * 0.5f);
                    Vector3 f = transform.TransformVector(Vector3.forward * 0.5f);
                    Vector3 u = transform.TransformVector(Vector3.up * 0.5f);

                    float uscale = transform.InverseTransformVector(transform.right).x * uvTiling.x;
                    float vscale = transform.InverseTransformVector(transform.up).y * uvTiling.y;
                    float uoffset = 0.5f * uvTiling.x - uvOffset.x;
                    float voffset = 0.5f * uvTiling.y + uvOffset.y;

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
                            Vector2 uv1 = new Vector2((vplane.GetDistanceToPoint(colliderVertex1) * uscale) + uoffset, (hplane.GetDistanceToPoint(colliderVertex1) * vscale) + voffset);
                            Vector2 uv2 = new Vector2((vplane.GetDistanceToPoint(colliderVertex2) * uscale) + uoffset, (hplane.GetDistanceToPoint(colliderVertex2) * vscale) + voffset);
                            Vector2 uv3 = new Vector2((vplane.GetDistanceToPoint(colliderVertex3) * uscale) + uoffset, (hplane.GetDistanceToPoint(colliderVertex3) * vscale) + voffset);

                            // undo our transformation so that the mesh looks correct in the scene.
                            colliderVertex1 = transform.InverseTransformPoint(colliderVertex1);
                            colliderVertex2 = transform.InverseTransformPoint(colliderVertex2);
                            colliderVertex3 = transform.InverseTransformPoint(colliderVertex3);

                            // ADD
                            // todo: calculate a z-offset using the sibling index letting you order decals in the hierarchy.
                            Plane plane = new Plane(colliderVertex1, colliderVertex2, colliderVertex3);
                            colliderVertex1 += plane.normal * 0.001f;
                            colliderVertex2 += plane.normal * 0.001f;
                            colliderVertex3 += plane.normal * 0.001f;

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
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.SetUVs(0, uvs);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
            }
        }

        private bool IsDirty()
        {
            return (transform.position != lastWorldPosition || transform.rotation != lastWorldRotation || transform.lossyScale != lastWorldScale);
        }

        private void UpdateDirtyFlags()
        {
            // remember the last world transformation.
            lastWorldPosition = transform.position;
            lastWorldRotation = transform.rotation;
            lastWorldScale = transform.lossyScale;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.white;
        }

        private void OnDrawGizmos()
        {
            Vector3 r = transform.TransformVector(Vector3.right * 0.5f);
            Vector3 f = transform.TransformVector(Vector3.forward * 0.5f);
            Vector3 u = transform.TransformVector(Vector3.up * 0.5f);

            // draw arrow pointing toward the projection.
            Vector3 point = transform.position - f;
            f.Normalize();

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(point, point - f);
            f = f.normalized * 0.25f;
            r = r.normalized * 0.25f;
            u = u.normalized * 0.25f;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(point, point + u);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(point, point - f - r);
            Gizmos.DrawLine(point, point - f + r);

            Gizmos.color = Color.white;
        }

        /// <summary>
        /// Resets the decal mesh and assigns it to the mesh filter.
        /// </summary>
        private Mesh ResetDecalMesh()
        {
            Mesh decalMesh = new Mesh();
            decalMesh.name = "Chisel Decal";
            meshFilter.sharedMesh = decalMesh;
            return decalMesh;
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
                Vector3 r = transform.TransformVector(Vector3.right * 0.5f);
                Vector3 f = transform.TransformVector(Vector3.forward * 0.5f);
                Vector3 u = transform.TransformVector(Vector3.up * 0.5f);

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