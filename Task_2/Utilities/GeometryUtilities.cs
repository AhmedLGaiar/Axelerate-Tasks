using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Task_2.Utilities
{
    internal class GeometryUtilities
    {
        public static XYZ FarthestPointFromDoor(XYZ doorPoint, Curve wallCurve)
        {
            XYZ start = wallCurve.GetEndPoint(0);
            XYZ end = wallCurve.GetEndPoint(1);

            double distanceToStart = doorPoint.DistanceTo(start);
            double distanceToEnd = doorPoint.DistanceTo(end);

            return (distanceToStart > distanceToEnd) ? start : end;
        }
        public static XYZ GetBestOrientation(Wall wall, XYZ placementPoint, XYZ doorPoint)
        {
            Curve wallCurve = ((LocationCurve)wall.Location).Curve;
            XYZ wallDirection = (wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0)).Normalize();

            // Two perpendicular options
            XYZ facingDir1 = wallDirection.CrossProduct(XYZ.BasisZ).Normalize();
            XYZ facingDir2 = (-wallDirection).CrossProduct(XYZ.BasisZ).Normalize();

            // Choose direction that's farthest from door
            double dist1 = (placementPoint + facingDir1).DistanceTo(doorPoint);
            double dist2 = (placementPoint + facingDir2).DistanceTo(doorPoint);

            return (dist1 > dist2) ? facingDir1 : facingDir2;
        }
    }
}
