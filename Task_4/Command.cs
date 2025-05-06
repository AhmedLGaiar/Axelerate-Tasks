using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
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
                Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element, new WallSelectionFilter(), "Pick a bathroom wall");
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
                    DrawVerticalLinesAtIntervals(doc, sketchPlane, wallCurve, wallHeight, 2.0);

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

        private void DrawVerticalLinesAtIntervals(Document doc, SketchPlane sketchPlane, Curve wallCurve, double wallHeight, double intervalInFeet)
        {
            double interval = UnitUtils.ConvertToInternalUnits(intervalInFeet, UnitTypeId.Feet);
            double wallLength = wallCurve.Length;
            int intervals = (int)Math.Floor(wallLength / interval);
            XYZ direction = (wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0)).Normalize();

            for (int i = 1; i <= intervals; i++)
            {
                XYZ pointOnWall = wallCurve.GetEndPoint(0) + direction * (interval * i);
                Line verticalLine = Line.CreateBound(pointOnWall, pointOnWall + XYZ.BasisZ * wallHeight);
                doc.Create.NewModelCurve(verticalLine, sketchPlane);
            }
        }
    }
}