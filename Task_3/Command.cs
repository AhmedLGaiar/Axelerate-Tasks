using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Task_3
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;

            try
            {
                FloorType genericFloor = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .FirstOrDefault(f => f.Name == "Generic 150mm");

                if (genericFloor == null)
                {
                    TaskDialog.Show("Warning", "Floor type 'Generic 150mm' not found.");
                    return Result.Failed;
                }

                Level level = doc.ActiveView.GenLevel;
                if (level == null)
                {
                    TaskDialog.Show("Error", "Active view has no associated level.");
                    return Result.Failed;
                }

                SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
                {
                    SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
                };

                List<Room> rooms = new FilteredElementCollector(doc)
                    .OfClass(typeof(SpatialElement))
                    .OfType<Room>()
                    .Where(r => r.Area > 1)
                    .ToList();

                using (Transaction tx = new Transaction(doc, "Create Floors with Thresholds"))
                {
                    tx.Start();

                    List<ElementId> tempSolids = new List<ElementId>();

                    foreach (var room in rooms)
                    {
                        IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);
                        if (boundaries == null || boundaries.Count == 0) continue;

                        CurveLoop outerLoop = new CurveLoop();
                        foreach (BoundarySegment segment in boundaries[0])
                        {
                            outerLoop.Append(segment.GetCurve());
                        }

                        double floorThickness = UnitUtils.ConvertToInternalUnits(150, UnitTypeId.Millimeters);
                        Solid roomSolid = GeometryCreationUtilities.CreateExtrusionGeometry(
                            new List<CurveLoop> { outerLoop },
                            XYZ.BasisZ.Negate(),
                            floorThickness);

                        List<Solid> thresholdSolids = new List<Solid>();
                        XYZ roomCenter = (room.Location as LocationPoint).Point;

                        var roomDoors = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_Doors)
                            .WhereElementIsNotElementType()
                            .OfClass(typeof(FamilyInstance))
                            .Cast<FamilyInstance>()
                            .Where(d =>
                            {
                                if (d.Location is LocationPoint locPoint)
                                {
                                    return room.IsPointInRoom(locPoint.Point) != null;
                                }
                                return false;
                            })
                            .ToList();

                        foreach (var door in roomDoors)
                        {
                            if (!(door.Location is LocationPoint doorLoc)) continue;

                            Wall hostWall = door.Host as Wall;
                            if (hostWall == null) continue;

                            Element type = doc.GetElement(door.GetTypeId());
                            double width = type.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();
                            double depth = hostWall.Width / 2.0;

                            Curve wallCurve = (hostWall.Location as LocationCurve).Curve;
                            XYZ wallDir = (wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0)).Normalize();
                            XYZ right = wallDir;
                            XYZ forward = door.FacingOrientation.Normalize();
                            XYZ location = doorLoc.Point;

                            // Decide threshold side (into the room)
                            XYZ doorToRoom = roomCenter - location;
                            bool facesRoom = forward.DotProduct(doorToRoom) > 0;
                            int direction = facesRoom ? 1 : -1;

                            XYZ offset = forward * depth * direction;

                            XYZ p1 = location + right * (width / 2);
                            XYZ p2 = location - right * (width / 2);
                            XYZ q1 = p1 + offset;
                            XYZ q2 = p2 + offset;

                            CurveLoop thresholdLoop = new CurveLoop();
                            thresholdLoop.Append(Line.CreateBound(p1, p2));
                            thresholdLoop.Append(Line.CreateBound(p2, q2));
                            thresholdLoop.Append(Line.CreateBound(q2, q1));
                            thresholdLoop.Append(Line.CreateBound(q1, p1));

                            Solid thresholdSolid = GeometryCreationUtilities.CreateExtrusionGeometry(
                                new List<CurveLoop> { thresholdLoop },
                                XYZ.BasisZ.Negate(),
                                floorThickness);

                            thresholdSolids.Add(thresholdSolid);
                        }

                        // Merge threshold solids into the room solid
                        Solid combinedSolid = roomSolid;
                        foreach (var thresholdSolid in thresholdSolids)
                        {
                            try
                            {
                                combinedSolid = BooleanOperationsUtils.ExecuteBooleanOperation(
                                    combinedSolid,
                                    thresholdSolid,
                                    BooleanOperationsType.Union);
                            }
                            catch { }
                        }

                        // Optional visual check — remove later
                        DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.SetShape(new List<GeometryObject> { combinedSolid });
                        tempSolids.Add(ds.Id);

                        PlanarFace bottomFace = null;
                        foreach (Face face in combinedSolid.Faces)
                        {
                            if (face is PlanarFace planarFace &&
                                planarFace.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate()))
                            {
                                bottomFace = planarFace;
                                break;
                            }
                        }

                        if (bottomFace != null)
                        {
                            List<CurveLoop> floorLoops = new List<CurveLoop>();
                            foreach (EdgeArray loop in bottomFace.EdgeLoops)
                            {
                                CurveLoop curveLoop = new CurveLoop();
                                foreach (Edge edge in loop)
                                {
                                    curveLoop.Append(edge.AsCurve());
                                }
                                floorLoops.Add(curveLoop);
                            }

                            if (floorLoops.Count > 0)
                            {
                                Floor.Create(doc, floorLoops, genericFloor.Id, level.Id);
                            }
                        }
                    }

                    doc.Delete(tempSolids);
                    tx.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
