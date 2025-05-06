using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using Task_2.Filters;

namespace Task_4
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Force user to pick a wall only
                Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element, new WallSelectionFilter(),
                    "Pick a bathroom wall");
                Wall pickedWall = doc.GetElement(pickedRef) as Wall;

                // Get the wall's location curve
                LocationCurve wallLocation = pickedWall.Location as LocationCurve;
                if (wallLocation == null)
                {
                    TaskDialog.Show("Error", "Selected wall doesn't have a location curve.");
                    return Result.Failed;
                }

                // Get the wall's curve
                Curve wallCurve = wallLocation.Curve;
                XYZ wallDirection = (wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0)).Normalize();
                XYZ wallNormal = XYZ.BasisZ.CrossProduct(wallDirection).Normalize();

                // Get the wall's height
                double wallHeight = pickedWall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();

                using (Transaction tx = new Transaction(doc, "Create Framing Lines"))
                {
                    tx.Start();

                    // Create a sketch plane aligned with the wall
                    Plane plane = Plane.CreateByNormalAndOrigin(wallNormal, wallCurve.GetEndPoint(0));
                    SketchPlane sketchPlane = SketchPlane.Create(doc, plane);

                    // Draw model lines along the wall's boundary
                    DrawWallBoundaryLines(doc, sketchPlane, wallCurve, wallHeight);

                    // Draw vertical lines at 2-foot intervals
                    DrawVerticalLinesAtIntervals(doc, sketchPlane, wallCurve, wallHeight, 2.0, pickedWall);

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

        private void DrawWallBoundaryLines(Document doc, SketchPlane sketchPlane, Curve wallCurve, double wallHeight)
        {
            XYZ startPoint = wallCurve.GetEndPoint(0);
            XYZ endPoint = wallCurve.GetEndPoint(1);

            // Create lines for bottom and top edges (along wall direction)
            Line bottomLine = Line.CreateBound(startPoint, endPoint);
            Line topLine = Line.CreateBound(startPoint + XYZ.BasisZ * wallHeight, endPoint + XYZ.BasisZ * wallHeight);

            // Create vertical lines at ends
            Line startVertical = Line.CreateBound(startPoint, startPoint + XYZ.BasisZ * wallHeight);
            Line endVertical = Line.CreateBound(endPoint, endPoint + XYZ.BasisZ * wallHeight);

            // Create the model lines
            doc.Create.NewModelCurve(bottomLine, sketchPlane);
            doc.Create.NewModelCurve(topLine, sketchPlane);
            doc.Create.NewModelCurve(startVertical, sketchPlane);
            doc.Create.NewModelCurve(endVertical, sketchPlane);
        }

        private void DrawVerticalLinesAtIntervals(
           Document doc,
           SketchPlane sketchPlane,
           Curve wallCurve,
           double wallHeight,
           double intervalInFeet,
           Wall pickedWall)
        {
            double interval = UnitUtils.ConvertToInternalUnits(intervalInFeet, UnitTypeId.Feet);
            double wallLength = wallCurve.Length;
            int intervals = (int)Math.Floor(wallLength / interval);
            XYZ direction = (wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0)).Normalize();
            XYZ startPoint = wallCurve.GetEndPoint(0);

            // Get the plane normal and origin
            XYZ planeNormal = sketchPlane.GetPlane().Normal;
            XYZ planeOrigin = sketchPlane.GetPlane().Origin;

            // Get all door and window openings hosted by the wall
            var openings = new FilteredElementCollector(doc)
                .WherePasses(new ElementClassFilter(typeof(FamilyInstance)))
                .WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_Doors))
                .Concat(new FilteredElementCollector(doc)
                    .WherePasses(new ElementClassFilter(typeof(FamilyInstance)))
                    .WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_Windows)))
                .Cast<FamilyInstance>()
                .Where(f => f.Host?.Id == pickedWall.Id)
                .Select(f => new {
                    BoundingBox = f.get_BoundingBox(null),
                    Instance = f,
                    IsDoor = f.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors
                })
                .Where(x => x.BoundingBox != null)
                .ToList();

            // First pass: Draw all regular vertical lines, skipping openings
            for (int i = 1; i <= intervals; i++)
            {
                XYZ pointOnWall = startPoint + direction * (interval * i);

                // Project the point onto the sketch plane
                XYZ projectedPoint = ProjectPointToPlane(pointOnWall, planeNormal, planeOrigin);

                bool isInsideOpening = openings.Any(opening =>
                {
                    XYZ min = opening.BoundingBox.Min;
                    XYZ max = opening.BoundingBox.Max;
                    return pointOnWall.X > min.X && pointOnWall.X < max.X &&
                           pointOnWall.Y > min.Y && pointOnWall.Y < max.Y;
                });

                if (!isInsideOpening)
                {
                    XYZ topPoint = projectedPoint + XYZ.BasisZ * wallHeight;
                    Line verticalLine = Line.CreateBound(projectedPoint, topPoint);
                    doc.Create.NewModelCurve(verticalLine, sketchPlane);
                }
            }

            // Second pass: Draw boundary lines around openings
            foreach (var opening in openings)
            {
                XYZ min = opening.BoundingBox.Min;
                XYZ max = opening.BoundingBox.Max;

                // Project all points to the sketch plane
                XYZ minProjected = ProjectPointToPlane(min, planeNormal, planeOrigin);
                XYZ maxProjected = ProjectPointToPlane(max, planeNormal, planeOrigin);

                // For doors only: skip the bottom line
                if (opening.IsDoor)
                {
                    // Top boundary line (horizontal)
                    XYZ topStart = new XYZ(minProjected.X, minProjected.Y, maxProjected.Z);
                    XYZ topEnd = new XYZ(maxProjected.X, maxProjected.Y, maxProjected.Z);
                    Line topLine = Line.CreateBound(topStart, topEnd);
                    doc.Create.NewModelCurve(topLine, sketchPlane);

                    // Left boundary line (vertical)
                    XYZ leftBottom = new XYZ(minProjected.X, minProjected.Y, minProjected.Z);
                    XYZ leftTop = new XYZ(minProjected.X, minProjected.Y, maxProjected.Z);
                    Line leftLine = Line.CreateBound(leftBottom, leftTop);
                    doc.Create.NewModelCurve(leftLine, sketchPlane);

                    // Right boundary line (vertical)
                    XYZ rightBottom = new XYZ(maxProjected.X, maxProjected.Y, minProjected.Z);
                    XYZ rightTop = new XYZ(maxProjected.X, maxProjected.Y, maxProjected.Z);
                    Line rightLine = Line.CreateBound(rightBottom, rightTop);
                    doc.Create.NewModelCurve(rightLine, sketchPlane);
                }
                else // For windows: draw all boundary lines
                {
                    // Top boundary line (horizontal)
                    XYZ topStart = new XYZ(minProjected.X, minProjected.Y, maxProjected.Z);
                    XYZ topEnd = new XYZ(maxProjected.X, maxProjected.Y, maxProjected.Z);
                    Line topLine = Line.CreateBound(topStart, topEnd);
                    doc.Create.NewModelCurve(topLine, sketchPlane);

                    // Bottom boundary line (horizontal)
                    XYZ bottomStart = new XYZ(minProjected.X, minProjected.Y, minProjected.Z);
                    XYZ bottomEnd = new XYZ(maxProjected.X, maxProjected.Y, minProjected.Z);
                    Line bottomLine = Line.CreateBound(bottomStart, bottomEnd);
                    doc.Create.NewModelCurve(bottomLine, sketchPlane);

                    // Left boundary line (vertical)
                    XYZ leftBottom = new XYZ(minProjected.X, minProjected.Y, minProjected.Z);
                    XYZ leftTop = new XYZ(minProjected.X, minProjected.Y, maxProjected.Z);
                    Line leftLine = Line.CreateBound(leftBottom, leftTop);
                    doc.Create.NewModelCurve(leftLine, sketchPlane);

                    // Right boundary line (vertical)
                    XYZ rightBottom = new XYZ(maxProjected.X, maxProjected.Y, minProjected.Z);
                    XYZ rightTop = new XYZ(maxProjected.X, maxProjected.Y, maxProjected.Z);
                    Line rightLine = Line.CreateBound(rightBottom, rightTop);
                    doc.Create.NewModelCurve(rightLine, sketchPlane);
                }
            }
        }

        private XYZ ProjectPointToPlane(XYZ point, XYZ planeNormal, XYZ planeOrigin)
        {
            // Vector from plane origin to point
            XYZ vector = point - planeOrigin;

            // Distance from point to plane along normal
            double distance = vector.DotProduct(planeNormal);

            // Projected point
            return point - distance * planeNormal;
        }
    }
}