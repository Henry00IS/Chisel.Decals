using System.Collections.Generic;
using UnityEngine;

namespace AeternumGames.Chisel.Decals
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ChiselDecal : MonoBehaviour
    {
#if UNITY_EDITOR

        /// <summary>Shared dictionary of octrees of triangle indices for every mesh we are computing with.</summary>
        internal static readonly Dictionary<Mesh, BoundsOctree<BoundsOctreeTriangle>> meshTriangleOctrees = new Dictionary<Mesh, BoundsOctree<BoundsOctreeTriangle>>();

        private MeshFilter meshFilter;
        private int lastInstanceID;

        [SerializeField] private Vector3 lastWorldPosition;
        [SerializeField] private Quaternion lastWorldRotation;
        [SerializeField] private Vector3 lastWorldScale;

        [SerializeField] private Vector2 uvTiling = new Vector2(1.0f, 1.0f);
        [SerializeField] private Vector2 uvOffset = new Vector2(0.0f, 0.0f);

        [Range(0.0f, 180.0f)]
        [SerializeField] private float maxAngle = 89.0f;

        private void OnEnable()
        {
            // no logic during play.
            if (Application.isPlaying) return;

            // mark the game object as static.
            gameObject.isStatic = true;

            // initialize the mesh renderer.
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ensure a material is assigned to the mesh renderer.
            if (meshRenderer.sharedMaterial == null)
                meshRenderer.sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Packages/com.aeternumgames.chisel.decals/Chisel/Decals/Materials/com.aeternumgames.chisel.decals.placeholder.mat");

            // initialize the mesh filter.
            meshFilter = GetComponent<MeshFilter>();
            meshFilter.hideFlags = HideFlags.NotEditable;

            // ensure a mesh is assigned to the mesh filter.
            if (meshFilter.sharedMesh == null)
                ResetDecalMesh();
        }

        private void Update()
        {
            // no logic during play.
            if (Application.isPlaying) return;

            // only rebuild the decal mesh if we were modified.
            if (!IsDirty()) return;
            // update the flags we need to tell if we have to rebuild the decal mesh.
            UpdateDirtyFlags();
            // rebuild the decal mesh.
            Rebuild();
        }

        internal void Rebuild()
        {
            // no logic during play.
            if (Application.isPlaying) return;

            // create a new mesh or clear the existing one.
            Mesh mesh = ResetDecalMesh();

            // update the flags we need to tell if we have to rebuild the decal mesh.
            UpdateDirtyFlags();

            // find all mesh colliders that intersect with the decal projector box.
            List<MeshCollider> meshColliders = FindMeshColliders();
            int meshCollidersCount = meshColliders.Count;
            if (meshCollidersCount == 0) return; // early out.

            List<Vector3> vertices = new List<Vector3>(6 * meshCollidersCount);
            List<int> triangles = new List<int>(6 * meshCollidersCount);
            List<Vector2> uvs = new List<Vector2>(6 * meshCollidersCount);

            // precalculate values that never change from this point on.

            Bounds projectorBounds = GetBounds();
            projectorBounds.center = transform.position - projectorBounds.center;

            Vector3 r = transform.TransformVector(Vector3.right * 0.5f);
            Vector3 f = transform.TransformVector(Vector3.forward * 0.5f);
            Vector3 u = transform.TransformVector(Vector3.up * 0.5f);

            float uscale = transform.InverseTransformVector(transform.right).x * uvTiling.x;
            float vscale = transform.InverseTransformVector(transform.up).y * uvTiling.y;
            float uoffset = 0.5f * uvTiling.x - uvOffset.x;
            float voffset = 0.5f * uvTiling.y + uvOffset.y;

            Plane[] clipPlanes = new Plane[] {
                new Plane(-r, transform.position + r),
                new Plane(r, transform.position - r),
                new Plane(-f, transform.position + f),
                new Plane(f, transform.position - f),
                new Plane(-u, transform.position + u + f),
                new Plane(u, transform.position - u + f)
            };

            // create a horizontal and vertical plane through the projector box.
            // the distance of the vertices to the planes are used to calculate UV coordinates.
            Plane hplane = new Plane(u, transform.position);
            Plane vplane = new Plane(r, transform.position);

            float maxAngleCos = Mathf.Cos(Mathf.Deg2Rad * (180.0f - maxAngle));

            // optimization: recycle the same polygon list used for clipping.
            List<Vector3> poly = new List<Vector3>(8);

            for (int mc = 0; mc < meshCollidersCount; mc++)
            {
                var meshCollider = meshColliders[mc];
                Mesh colliderMesh = meshCollider.sharedMesh;

                // make sure an octree of this mesh exists.
                if (!meshTriangleOctrees.ContainsKey(colliderMesh))
                {
                    meshTriangleOctrees.Add(colliderMesh, new BoundsOctree<BoundsOctreeTriangle>(8.0f, meshCollider.transform.position, 4.0f, 1.0f));

                    // get the collider vertices and triangles.
                    Vector3[] colliderVertices = colliderMesh.vertices;
                    int[] colliderTriangles = colliderMesh.GetTriangles(0);

                    // build the octree.
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

                        // calculate the triangle bounds.
                        Bounds triangleBounds = new Bounds(colliderVertex1, Vector3.zero);
                        triangleBounds.Encapsulate(colliderVertex2);
                        triangleBounds.Encapsulate(colliderVertex3);

                        // add the triangle to the octree.
                        meshTriangleOctrees[colliderMesh].Add(new BoundsOctreeTriangle() { triangleVertex1 = colliderVertex1, triangleVertex2 = colliderVertex2, triangleVertex3 = colliderVertex3 }, triangleBounds);
                    }
                }

                // find all triangles inside of the projector bounds using the octree.
                List<BoundsOctreeTriangle> trianglesInsideProjector = new List<BoundsOctreeTriangle>();
                meshTriangleOctrees[colliderMesh].GetColliding(trianglesInsideProjector, projectorBounds);
                int trianglesInsideProjectorCount = trianglesInsideProjector.Count;

                // optimization: ensure the decal mesh list capacities are large enough to contain all
                // of the triangles. By themselves the lists would reallocate their array many times.
                if (vertices.Capacity < trianglesInsideProjectorCount)
                {
                    vertices.Capacity = trianglesInsideProjectorCount;
                    triangles.Capacity = trianglesInsideProjectorCount;
                    uvs.Capacity = trianglesInsideProjectorCount;
                }

                // iterate over all triangles inside of the projector bounds:
                for (int i = 0; i < trianglesInsideProjectorCount; i++)
                {
                    // fetch a triangle from the octree.
                    BoundsOctreeTriangle triangle = trianglesInsideProjector[i];
                    Vector3 colliderVertex1 = triangle.triangleVertex1;
                    Vector3 colliderVertex2 = triangle.triangleVertex2;
                    Vector3 colliderVertex3 = triangle.triangleVertex3;

                    // the mesh vertices are in world space 1:1 to how they appear in the scene.

                    // if the triangle exceeds the maximum angle we discard it.
                    if (Vector3.Dot(transform.forward, GetNormal(colliderVertex1, colliderVertex2, colliderVertex3)) >= maxAngleCos)
                        continue;

                    // optimization?: if the triangle is wholly inside of the projector we don't have to clip it.

                    // clip the triangle to fit inside of the projector box.
                    poly.Clear();
                    poly.AddRange(new Vector3[] { colliderVertex1, colliderVertex2, colliderVertex3 });
                    ClipTriangle(poly, clipPlanes);

                    if (poly.Count >= 3)
                    {
                        Vector3[] triangulated;
                        // only triangulate if required:
                        if (poly.Count > 3)
                            triangulated = Triangulate(poly);
                        else
                            triangulated = new Vector3[] { poly[0], poly[1], poly[2] };

                        for (int tr = 0; tr < triangulated.Length; tr += 3)
                        {
                            colliderVertex1 = triangulated[tr + 0];
                            colliderVertex2 = triangulated[tr + 1];
                            colliderVertex3 = triangulated[tr + 2];

                            // use the horizontal and vertical plane through the projector box.
                            // the distance of the vertices to the planes are used to calculate UV coordinates.
                            Vector2 uv1 = new Vector2((vplane.GetDistanceToPoint(colliderVertex1) * uscale) + uoffset, (hplane.GetDistanceToPoint(colliderVertex1) * vscale) + voffset);
                            Vector2 uv2 = new Vector2((vplane.GetDistanceToPoint(colliderVertex2) * uscale) + uoffset, (hplane.GetDistanceToPoint(colliderVertex2) * vscale) + voffset);
                            Vector2 uv3 = new Vector2((vplane.GetDistanceToPoint(colliderVertex3) * uscale) + uoffset, (hplane.GetDistanceToPoint(colliderVertex3) * vscale) + voffset);

                            // undo our transformation so that the mesh looks correct in the scene.
                            colliderVertex1 = transform.InverseTransformPoint(colliderVertex1);
                            colliderVertex2 = transform.InverseTransformPoint(colliderVertex2);
                            colliderVertex3 = transform.InverseTransformPoint(colliderVertex3);

                            // ADD
                            // todo: calculate a z-offset using the sibling index letting you order decals in the hierarchy.
                            Vector3 normal = GetNormal(colliderVertex1, colliderVertex2, colliderVertex3);
                            colliderVertex1 += normal * 0.001f;
                            colliderVertex2 += normal * 0.001f;
                            colliderVertex3 += normal * 0.001f;

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
            Mesh decalMesh;

            // duplication will copy our shared mesh - detect that using the instance id.
            if (lastInstanceID == 0)
            {
                lastInstanceID = GetInstanceID();
                decalMesh = new Mesh();
                decalMesh.name = "Chisel Decal";
                meshFilter.sharedMesh = decalMesh;
                return decalMesh;
            }

            // clear the current mesh to prevent allocating a new one every rebuild.
            decalMesh = meshFilter.sharedMesh;
            decalMesh.Clear();
            return decalMesh;
        }

        /// <summary>
        /// Finds all mesh colliders that intersect with the decal projector box.
        /// </summary>
        /// <param name="results">The list that will contain all of the results.</param>
        private List<MeshCollider> FindMeshColliders()
        {
            Collider[] colliders = Physics.OverlapBox(transform.position, transform.lossyScale * 0.5f, Quaternion.LookRotation(transform.forward));
            var results = new List<MeshCollider>(colliders.Length);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider.GetType() == typeof(MeshCollider))
                    results.Add((MeshCollider)collider);
            }
            return results;
        }

        private static Vector3[] Triangulate(List<Vector3> polygon)
        {
            int triangleCount = polygon.Count - 2;
            var triangles = new Vector3[triangleCount * 3];

            // Calculate triangulation
            int i = 0;
            for (int j = 0; j < triangleCount; j++)
            {
                triangles[i++] = polygon[0];
                triangles[i++] = polygon[j + 1];
                triangles[i++] = polygon[j + 2];
            }

            return triangles;
        }

        private Bounds GetBounds()
        {
            Vector3 r = transform.TransformVector(Vector3.right * 0.5f);
            Vector3 f = transform.TransformVector(Vector3.forward * 0.5f);
            Vector3 u = transform.TransformVector(Vector3.up * 0.5f);

            Bounds projectorBounds = new Bounds();
            projectorBounds.Encapsulate(f + r + u);
            projectorBounds.Encapsulate(f - r + u);
            projectorBounds.Encapsulate(f + r - u);
            projectorBounds.Encapsulate(f - r - u);
            projectorBounds.Encapsulate(-f + r + u);
            projectorBounds.Encapsulate(-f - r + u);
            projectorBounds.Encapsulate(-f + r - u);
            projectorBounds.Encapsulate(-f - r - u);
            return projectorBounds;
        }

        // warning: input vertices will be modified!
        private static void ClipTriangle(List<Vector3> vertices, Plane[] clippingPlanes)
        {
            //Save the new vertices temporarily in this array before transfering them to vertices
            Vector3[] vertices_tmp = new Vector3[8];
            int vertices_tmp_count = 0;

            //Clip the polygon
            for (int i = 0; i < clippingPlanes.Length; i++)
            {
                Plane plane = clippingPlanes[i];
                Vector3 planePosition = plane.ClosestPointOnPlane(Vector3.zero);

                for (int j = 0; j < vertices.Count; j++)
                {
                    int jPlusOne = (j + 1) % vertices.Count;

                    Vector3 v1 = vertices[j];
                    Vector3 v2 = vertices[jPlusOne];

                    //Calculate the distance to the plane from each vertex
                    //This is how we will know if they are inside or outside
                    //If they are inside, the distance is positive, which is why the planes normals have to be oriented to the inside
                    float dist_to_v1 = plane.GetDistanceToPoint(v1);
                    float dist_to_v2 = plane.GetDistanceToPoint(v2);

                    //Case 1. Both are outside (= to the right), do nothing
                    if (dist_to_v1 < 0f && dist_to_v2 < 0f) continue;

                    //Case 2. Both are inside (= to the left), save v2
                    if (dist_to_v1 > 0f && dist_to_v2 > 0f)
                    {
                        vertices_tmp[vertices_tmp_count++] = v2;
                    }
                    //Case 3. Outside -> Inside, save intersection point and v2
                    else if (dist_to_v1 < 0f && dist_to_v2 > 0f)
                    {
                        Vector3 rayDir = (v2 - v1).normalized;

                        Vector3 intersectionPoint = GetRayPlaneIntersectionCoordinate(planePosition, plane.normal, v1, rayDir);

                        vertices_tmp[vertices_tmp_count++] = intersectionPoint;

                        vertices_tmp[vertices_tmp_count++] = v2;
                    }
                    //Case 4. Inside -> Outside, save intersection point
                    else if (dist_to_v1 > 0f && dist_to_v2 < 0f)
                    {
                        Vector3 rayDir = (v2 - v1).normalized;

                        Vector3 intersectionPoint = GetRayPlaneIntersectionCoordinate(planePosition, plane.normal, v1, rayDir);

                        vertices_tmp[vertices_tmp_count++] = intersectionPoint;
                    }
                }

                //Add the new vertices to the list of vertices
                vertices.Clear();

                for (int k = 0; k < vertices_tmp_count; k++)
                    vertices.Add(vertices_tmp[k]);

                vertices_tmp_count = 0; // vertices_tmp.Clear();
            }
        }

        //Get the coordinate if we know a ray-plane is intersecting
        private static Vector3 GetRayPlaneIntersectionCoordinate(Vector3 planePos, Vector3 planeNormal, Vector3 rayStart, Vector3 rayDir)
        {
            float denominator = Vector3.Dot(-planeNormal, rayDir);

            Vector3 vecBetween = planePos - rayStart;

            float t = Vector3.Dot(vecBetween, -planeNormal) / denominator;

            Vector3 intersectionPoint = rayStart + rayDir * t;

            return intersectionPoint;
        }

        // Get the normal to a triangle from the three corner points, a, b and c.
        private static Vector3 GetNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            // Find vectors corresponding to two of the sides of the triangle.
            // Cross the vectors to get a perpendicular vector, then normalize it.
            return Vector3.Cross(b - a, c - a).normalized;
        }

#endif
    }
}