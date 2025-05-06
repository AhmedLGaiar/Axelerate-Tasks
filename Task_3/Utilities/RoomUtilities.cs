using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Task_3.Utilities
{
    internal static class RoomUtilities
    {
        public static FloorType GetFloorTypeByName(this Document doc, string Name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .OfCategory(BuiltInCategory.OST_Floors)
                .Cast<FloorType>()
                .FirstOrDefault(f => f.Name == Name);
        }
        public static List<Room> GetAllRooms(this Document doc)
        {
           return new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement))
                .OfType<Room>()
                .Where(r => r.Area > 1)
                .ToList();
        }
        public  static double GetDoorWidth(Document doc,FamilyInstance door)
        {
            // Get the column type
            ElementId typeId = door.GetTypeId();
            ElementType colType = doc.GetElement(typeId) as ElementType;

            // Get width and length from parameters
            double width = colType?.LookupParameter("Width")?.AsDouble() ?? 0;
            return width;
        }
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