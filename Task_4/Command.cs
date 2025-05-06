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

            // Get bounding boxes of door/window openings
            List<BoundingBoxXYZ> openings = new FilteredElementCollector(doc)
                .WherePasses(new ElementClassFilter(typeof(FamilyInstance)))
                .WhereElementIsNotElementType()
                .Where(e => e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors ||
                            e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Windows)
                .Cast<FamilyInstance>()
                .Where(f => f.Host?.Id == pickedWall.Id)
                .Select(f => f.get_BoundingBox(null))
                .Where(bb => bb != null)
                .ToList();

            for (int i = 1; i <= intervals; i++)
            {
                XYZ pointOnWall = startPoint + direction * (interval * i);
                XYZ basePoint = pointOnWall;
                XYZ topPoint = basePoint + XYZ.BasisZ * wallHeight;

                // Check if point intersects any opening in X/Y, and get top Z of opening
                BoundingBoxXYZ intersectingOpening = openings.FirstOrDefault(bbox =>
                {
                    XYZ min = bbox.Min;
                    XYZ max = bbox.Max;

                    return pointOnWall.X >= min.X && pointOnWall.X <= max.X &&
                           pointOnWall.Y >= min.Y && pointOnWall.Y <= max.Y;
                });

                if (intersectingOpening != null)
                {
                    // Draw only above the opening
                    double openingTopZ = intersectingOpening.Max.Z;
                    if (openingTopZ < topPoint.Z)
                    {
                        Line aboveOpening = Line.CreateBound(
                            new XYZ(basePoint.X, basePoint.Y, openingTopZ),
                            new XYZ(topPoint.X, topPoint.Y, topPoint.Z));
                        doc.Create.NewModelCurve(aboveOpening, sketchPlane);
                    }
                }
                else
                {
                    // No opening, draw full height
                    Line fullStud = Line.CreateBound(basePoint, topPoint);
                    doc.Create.NewModelCurve(fullStud, sketchPlane);
                }
            }
        }
    }
}