using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using Task_2.Data;

namespace Task_2.Utilities
{
    internal class RoomUtilities
    {
        public static Room GetBathroomsAttachedToWall(Document doc, Wall wall
            , out List<BoundarySegment> touchingSegments)
        {
            touchingSegments = new();

            // Keywords to match various bathroom naming conventions
            string[] keywords = ["bath", "toilet", "wc", "lavatory", "restroom"];

            // Boundary options for accurate wall association
            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };

            // Filter rooms whose name contains any of the keywords
            Room room = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .FirstOrDefault(r => keywords.Any(k =>
                    !string.IsNullOrWhiteSpace(r.Name) &&
                    r.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));

            
            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);
            if (boundaries == null) return null;

            foreach (IList<BoundarySegment> loop in boundaries)
            {
                foreach (BoundarySegment segment in loop)
                {
                    if (IsWallSegmentOfSelectedWall( doc, segment, wall))
                    {
                        touchingSegments.Add(segment);
                    }
                }
            }

            return touchingSegments.Any() ? room : null;
        }
        private static bool IsWallSegmentOfSelectedWall(Document doc ,BoundarySegment boundSeg, Wall selectedWall)
        {
            Element e = doc.GetElement(boundSeg.ElementId);
            Wall wall = e as Wall;
            return wall != null && wall.Id == selectedWall.Id;
        }

        public static FamilyInstance FindDoorFacingRoom(Document doc, Room room)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .FirstOrDefault(d =>
                {
                    Room toRoom = d.ToRoom;
                    Room fromRoom = d.FromRoom;

                    return (toRoom != null && toRoom.Id == room.Id) ||
                           (fromRoom != null && fromRoom.Id == room.Id);
                });
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

        /// <summary>
        /// Finds a door whose FromRoom or ToRoom matches the given room.
        /// </summary>
        public static FamilyInstance FindDoorInRoom(Document doc, Room room)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .FirstOrDefault(d => { return (d.ToRoom?.Id == room.Id || d.FromRoom?.Id == room.Id); });
        }
    }
}