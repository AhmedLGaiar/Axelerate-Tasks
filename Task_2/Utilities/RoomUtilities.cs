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
            Room roomsFound = null;

            // Keywords to match various bathroom naming conventions
            string[] keywords = ["bath", "toilet", "wc", "lavatory", "restroom"];

            // Boundary options for accurate wall association
            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };

            // Filter rooms whose name contains any of the keywords
            var rooms = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .Where(r => keywords.Any(k =>
                    !string.IsNullOrWhiteSpace(r.Name) &&
                    r.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            foreach (var room in rooms)
            {
                foreach (IList<BoundarySegment> loop in room.GetBoundarySegments(options))
                {
                    foreach (BoundarySegment segment in loop)
                    {
                        if (wall.Id == segment.ElementId)
                        {
                            touchingSegments.Add(segment);
                            roomsFound = room;
                        }
                    }
                }
            }

            return touchingSegments.Any() ? roomsFound : null;
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