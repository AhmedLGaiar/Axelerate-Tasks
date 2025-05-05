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

                using (Transaction tx = new Transaction(doc, "Create Floors with Merged Thresholds"))
                {
                    tx.Start();

                    // Collect all temporary solids to delete later
                    List<ElementId> tempSolids = new List<ElementId>();

                    foreach (var room in rooms)
                    {
                        // 1. Create room solid
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

                        // 2. Create threshold solids for doors in this room
                        List<Solid> thresholdSolids = new List<Solid>();
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
                            LocationPoint doorLoc = door.Location as LocationPoint;
                            if (doorLoc == null) continue;

                            Wall hostWall = door.Host as Wall;
                            if (hostWall == null) continue;

                            ElementId typeId = door.GetTypeId();
                            Element type = doc.GetElement(typeId);
                            double width = type.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();
                            double depth = hostWall.Width / 2.0;

                            Curve wallCurve = (hostWall.Location as LocationCurve).Curve;
                            XYZ wallDir = (wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0)).Normalize();
                            XYZ right = wallDir;
                            XYZ forward = door.FacingOrientation.Normalize();
                            XYZ location = doorLoc.Point;

                            // Points along the door width
                            XYZ p1 = location + right * (width / 2);
                            XYZ p2 = location - right * (width / 2);

                            // Generate threshold geometry (only front side)
                            XYZ offset = forward * depth;
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

                        // 3. Combine all solids
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
                            catch { /* Ignore union failures */ }
                        }

                        // 4. Create direct shape for visualization (temporary)
                        DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.SetShape(new List<GeometryObject> { combinedSolid });
                        tempSolids.Add(ds.Id);

                        // 5. Get the bottom face of the combined solid for floor creation
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
                            // 6. Get the edge loops from the bottom face
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

                            // 7. Create the actual floor
                            if (floorLoops.Count > 0)
                            {
                                Floor.Create(doc, floorLoops, genericFloor.Id, level.Id);
                            }
                        }
                    }

                    // Delete all temporary solids
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