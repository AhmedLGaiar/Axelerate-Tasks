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
                        Wall hostWall = door.Host as Wall;
                        if (hostWall == null) continue;

                        XYZ location = doorLoc.Point;
                        double width = type.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();
                        double depth = hostWall.Width / 2.0;

                        Curve wallCurve = (hostWall.Location as LocationCurve).Curve;
                        XYZ wallDir = (wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0)).Normalize();
                        XYZ right = wallDir;
                        XYZ forward = door.FacingOrientation.Normalize();
                        XYZ up = XYZ.BasisZ;

                        // Points along the door width
                        XYZ p1 = location + right * (width / 2);
                        XYZ p2 = location - right * (width / 2);

                        // Generate 2 bump-out floors: one on each side
                        foreach (int dirSign in new[] { 1, -1 }) // +1 = front, -1 = back
                        {
                            XYZ offset = forward * depth * dirSign;

                            XYZ q1 = p1 + offset;
                            XYZ q2 = p2 + offset;

                            Line l1 = Line.CreateBound(p1, p2);
                            Line l2 = Line.CreateBound(p2, q2);
                            Line l3 = Line.CreateBound(q2, q1);
                            Line l4 = Line.CreateBound(q1, p1);

                            CurveLoop loop = new CurveLoop();
                            loop.Append(l1);
                            loop.Append(l2);
                            loop.Append(l3);
                            loop.Append(l4);

                            Floor.Create(doc, new List<CurveLoop> { loop }, genericFloor.Id, hostWall.LevelId);
                        }
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