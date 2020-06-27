using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AeternumGames.Chisel.Decals
{
    internal class Clipping
    {
        private enum PointToPlaneRelation
        {
            PointInFrontOfPlane,
            PointBehindOfPlane,
            PointOnPlane
        }

        // Classify point p to a plane thickened by a given thickness epsilon
        private static PointToPlaneRelation ClassifyPointToPlane(Vector3 p, Plane plane)
        {
            // Compute signed distance of point from plane
            float dist = Vector3.Dot(plane.normal, p) - plane.distance;
            // Classify p based on the signed distance
            if (dist > 0.001f)
                return PointToPlaneRelation.PointInFrontOfPlane;
            if (dist < -0.001f)
                return PointToPlaneRelation.PointBehindOfPlane;
            return PointToPlaneRelation.PointOnPlane;
        }

        private enum PolygonToPlaneRelation
        {
            PolygonOnPlane,
            PolygonInFrontOfPlane,
            PolygonBehindOfPlane,
            PolygonCoplanarWithPlane
        }

        // Return value specifying whether the polygon ‘poly’ lies in front of,
        // behind of, on, or straddles the plane ‘plane’
        private PolygonToPlaneRelation ClassifyPolygonToPlane(Vector3[] poly, Plane plane)
        {
            // Loop over all polygon vertices and count how many vertices
            // lie in front of and how many lie behind of the thickened plane
            int numInFront = 0, numBehind = 0;
            int numVerts = poly.Length;
            for (int i = 0; i < numVerts; i++)
            {
                Vector3 p = poly[i];
                switch (ClassifyPointToPlane(p, plane))
                {
                    case PointToPlaneRelation.PointInFrontOfPlane:
                        numInFront++;
                        break;

                    case PointToPlaneRelation.PointBehindOfPlane:
                        numBehind++;
                        break;
                }
            }
            // If vertices on both sides of the plane, the polygon is straddling
            if (numBehind != 0 && numInFront != 0)
                return PolygonToPlaneRelation.PolygonOnPlane;
            // If one or more vertices in front of the plane and no vertices behind
            // the plane, the polygon lies in front of the plane
            if (numInFront != 0)
                return PolygonToPlaneRelation.PolygonInFrontOfPlane;
            // Ditto, the polygon lies behind the plane if no vertices in front of
            // the plane, and one or more vertices behind the plane
            if (numBehind != 0)
                return PolygonToPlaneRelation.PolygonBehindOfPlane;
            // All vertices lie on the plane so the polygon is coplanar with the plane
            return PolygonToPlaneRelation.PolygonCoplanarWithPlane;
        }

        /// <summary>
        /// Gets the normalized interpolant between <paramref name="point1"/> and <paramref name="point2"/> where the edge they
        /// represent intersects with the supplied <paramref name="plane"/>.
        /// </summary>
        /// <param name="plane">The plane that intersects with the edge.</param>
        /// <param name="point1">The first point of the edge.</param>
        /// <param name="point2">The last point of the edge.</param>
        /// <returns>The normalized interpolant between the edge points where the plane intersects.</returns>
        private static float GetPlaneIntersectionInterpolant(Plane plane, Vector3 point1, Vector3 point2)
        {
            Vector3 normal = plane.normal;
            return (-normal.x * point1.x - normal.y * point1.y - normal.z * point1.z - plane.distance) / (-normal.x * (point1.x - point2.x) - normal.y * (point1.y - point2.y) - normal.z * (point1.z - point2.z));
        }

        /// <summary>
        /// Splits the polygon.
        /// </summary>
        /// <param name="poly">The poly.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="frontPoly">The front poly.</param>
        /// <param name="backPoly">The back poly.</param>
        public static void SplitPolygon(Vector3[] poly, Plane plane, out Vector3[] frontPoly, out Vector3[] backPoly)
        {
            List<Vector3> frontVerts = new List<Vector3>(4);
            List<Vector3> backVerts = new List<Vector3>(4);
            // Test all edges (a, b) starting with edge from last to first vertex
            int numVerts = poly.Length;
            Vector3 a = poly[numVerts - 1];
            PointToPlaneRelation aSide = ClassifyPointToPlane(a, plane);
            // Loop over all edges given by vertex pair (n - 1, n)
            for (int n = 0; n < numVerts; n++)
            {
                Vector3 b = poly[n];
                PointToPlaneRelation bSide = ClassifyPointToPlane(b, plane);
                if (bSide == PointToPlaneRelation.PointInFrontOfPlane)
                {
                    if (aSide == PointToPlaneRelation.PointBehindOfPlane)
                    {
                        // Edge (a, b) straddles, output intersection point to both sides
                        // Consistently clip edge as ordered going from in front -> behind
                        //Vector3 i = new Vector3 { Position = FixVector3.Lerp(b.Position, a.Position, GetPlaneIntersectionInterpolant(plane, b.Position, a.Position)) };
                        Vector3 i = Vector3.Lerp(b, a, GetPlaneIntersectionInterpolant(plane, b, a));
                        //assert(ClassifyPointToPlane(i, plane) == PointToPlaneRelation.PointOnPlane);
                        frontVerts.Add(i);
                        backVerts.Add(i);
                    }
                    // In all three cases, output b to the front side
                    frontVerts.Add(b);
                }
                else if (bSide == PointToPlaneRelation.PointBehindOfPlane)
                {
                    if (aSide == PointToPlaneRelation.PointInFrontOfPlane)
                    {
                        // Edge (a, b) straddles plane, output intersection point
                        Vector3 i = Vector3.Lerp(a, b, GetPlaneIntersectionInterpolant(plane, a, b));
                        //Vector3 i = new Vector3 { Position = FixVector3.Lerp(a.Position, b.Position, GetPlaneIntersectionInterpolant(plane, a.Position, b.Position)) };
                        //assert(ClassifyPointToPlane(i, plane) == POINT_ON_PLANE);
                        frontVerts.Add(i);
                        backVerts.Add(i);
                    }
                    else if (aSide == PointToPlaneRelation.PointOnPlane)
                    {
                        // Output a when edge (a, b) goes from ‘on’ to ‘behind’ plane
                        backVerts.Add(a);
                    }
                    // In all three cases, output b to the back side
                    backVerts.Add(b);
                }
                else
                {
                    // b is on the plane. In all three cases output b to the front side
                    frontVerts.Add(b);
                    // In one case, also output b to back side
                    if (aSide == PointToPlaneRelation.PointBehindOfPlane)
                        backVerts.Add(b);
                }
                // Keep b as the starting point of the next edge
                a = b;
                aSide = bSide;
            }

            // Create (and return) two new polygons from the two vertex lists
            frontPoly = frontVerts.ToArray();
            backPoly = backVerts.ToArray();
        }
    }
}