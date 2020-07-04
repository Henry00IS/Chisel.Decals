#if UNITY_EDITOR

using UnityEngine;

namespace AeternumGames.Chisel.Decals
{
    // clipping algorithm for exactly 8 planes and a single triangle.
    // special thanks to Erik Nordeus https://www.habrador.com/tutorials/math/12-cut-polygons/ for his Sutherland-Hodgman algorithm.
    internal class Clipping
    {
        private Plane[] clippingPlanes;
        private Vector3[] clippingPlanePositions;
        private int clippingPlanesLength;
        private Vector3[] vertices_tmp = new Vector3[8];

        public Clipping(Plane[] planes)
        {
            clippingPlanes = planes;
            clippingPlanesLength = clippingPlanes.Length;

            // optimization: pre-calculate clip plane positions to prevent a dot product during clipping:
            clippingPlanePositions = new Vector3[clippingPlanesLength];
            for (int i = 0; i < clippingPlanesLength; i++)
                clippingPlanePositions[i] = clippingPlanes[i].ClosestPointOnPlane(Vector3.zero);
        }

        // warning: input vertices will be modified!
        public int ClipTriangle(Vector3[] vertices)
        {
            // we know there are always 3 vertices at first.
            int vertices_count = 3;

            // save the new vertices temporarily in this array before transfering them to vertices.
            int vertices_tmp_count = 0;

            //Clip the polygon
            for (int i = 0; i < clippingPlanes.Length; i++)
            {
                Plane plane = clippingPlanes[i];
                Vector3 planePosition = clippingPlanePositions[i];

                for (int j = 0; j < vertices_count; j++)
                {
                    int jPlusOne = (j + 1) % vertices_count;

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
                vertices_count = vertices_tmp_count;
                vertices_tmp.CopyTo(vertices, 0);

                vertices_tmp_count = 0;
            }

            return vertices_count;
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
    }
}

#endif