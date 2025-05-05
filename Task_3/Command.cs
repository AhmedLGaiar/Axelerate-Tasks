using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using Task_3.Data;

namespace Task_3
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            APPData.Initialize(commandData.Application);
            Document doc = APPData.Current.Doc;
            UIDocument uidoc = APPData.Current.UIDoc;
            try
            {
                FloorType genericFloor = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .OfCategory(BuiltInCategory.OST_Floors)
                    .Cast<FloorType>()
                    .FirstOrDefault(f => f.Name == "Generic 150mm");
                if (genericFloor == null)
                {
                    TaskDialog.Show("Warning", "FamilySymbol Floor 'Generic 150mm' was not found in the project.");
                    return Result.Failed;
                }

                Level level = doc.ActiveView.GenLevel;

                // Boundary options for accurate wall association
                SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
                {
                    SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
                };

                // Filter rooms whose name contains any of the keywords
                List<Room> rooms = new FilteredElementCollector(doc)
                    .OfClass(typeof(SpatialElement))
                    .OfType<Room>()
                    .Where(r => r.Area > 1)
                    .ToList();
                List<Room> bathrooms = new List<Room>();

                using (Transaction tx = new Transaction(doc, "In-Room Threshold"))
                {
                    tx.Start();
                    foreach (var room in rooms)
                    {
                        IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);
                        if (boundaries == null || boundaries.Count == 0) continue;

                        CurveLoop outerLoop = new CurveLoop();
                        foreach (BoundarySegment segment in boundaries[0])
                        {
                            outerLoop.Append(segment.GetCurve());
                        }

                        List<CurveLoop> loops = new List<CurveLoop> { outerLoop };
                        Floor.Create(doc, loops, genericFloor.Id, level.Id);
                    }

                    var allDoors = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType()
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .ToList();

                    foreach (var door in allDoors)
                    {
                        LocationPoint doorLoc = door.Location as LocationPoint;
                        if (doorLoc == null) continue;

                        ElementId typeId = door.GetTypeId();
                        Element type = doc.GetElement(typeId);
                        Wall hostWall = doc.GetElement(door.Host.Id) as Wall;

                        if (hostWall == null) continue;
                        XYZ Location = (door.Location as LocationPoint).Point;
                        double Width = type.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();
                        double Depth = hostWall.Width / 2.0;

                        Curve curve = (hostWall.Location as LocationCurve).Curve;
                        XYZ WallDirection = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();

                        XYZ p1 = Location + WallDirection * Width / 2.0;
                        XYZ p2 = Location + WallDirection.Negate() * Width / 2.0;

                        XYZ FaceDirection = door.FacingOrientation;
                        XYZ roomLocation = (door.Room.Location as LocationPoint).Point;
                        XYZ fromDoorToRoomCenter = (roomLocation - Location).Normalize();

                        XYZ p3;
                        XYZ p4;
                        if (FaceDirection.DotProduct(fromDoorToRoomCenter) > 0)
                        {
                            p3 = p2 + FaceDirection * Depth;
                            p4 = p1 + FaceDirection * Depth;
                        }
                        else
                        {
                            p3 = p2 - FaceDirection * Depth;
                            p4 = p1 - FaceDirection * Depth;
                        }

                        Line line1 = Line.CreateBound(p1, p2);
                        Line line2 = Line.CreateBound(p2, p3);
                        Line line3 = Line.CreateBound(p3, p4);
                        Line line4 = Line.CreateBound(p4, p1);

                        CurveLoop curves = new CurveLoop();
                        curves.Append(line1);
                        curves.Append(line2);
                        curves.Append(line3);
                        curves.Append(line4);

                        Floor.Create(doc, new List<CurveLoop> { curves }, genericFloor.Id, level.Id);
                    }


                    tx.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}