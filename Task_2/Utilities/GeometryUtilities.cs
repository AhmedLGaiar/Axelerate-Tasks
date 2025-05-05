using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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
        public static XYZ GetBestOrientation(Room room, XYZ point, Curve wallCurve, out XYZ roomCentroid)
        {
            roomCentroid = (room.Location as LocationPoint).Point;
            XYZ directionVector = roomCentroid - point;

            if (IsVerticalCurve(wallCurve))
            {
                directionVector = new XYZ(Math.Sign(directionVector.X), 0, 0);
            }
            else
            {
                directionVector = new XYZ(0, Math.Sign(directionVector.Y), 0);
            }

            return directionVector;
        }
        public static bool IsVerticalCurve(Curve curve)
        {
            return Math.Abs(curve.GetEndPoint(0).X - curve.GetEndPoint(1).X) < 0.001;
        }

        /// <summary>
        /// Retrieves the portion of a wall's boundary curve that lies inside the specified room,
        /// based on the provided boundary segments.
        /// </summary>
        /// <param name="boundarySegmentList">
        /// A list of boundary segments from the room that potentially include the wall.
        /// </param>
        /// <param name="wallElement">
        /// The wall whose interior-facing boundary curve is to be retrieved.
        /// </param>
        /// <returns>
        /// The <see cref="Curve"/> representing the wall's segment inside the room,
        /// or <c>null</c> if the wall is not found in the boundary segments.
        /// </returns>
        public static Curve GetWallCurveInsideRoom(IList<BoundarySegment> boundarySegmentList, Wall wallElement)
        {
            BoundarySegment matchingSegment = boundarySegmentList
                .FirstOrDefault(boundSeg => boundSeg.ElementId == wallElement.Id);

            return matchingSegment?.GetCurve();
        }
    }
}
