using System;
using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Task_1.Utilities
{
    internal static class LinesUtilities
    {
        internal static void DrawWallBoundaryLines(Document doc, SketchPlane sketchPlane, Curve wallCurve,
            double wallHeight)
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

        internal static void DrawVerticalLinesAtIntervals(Document doc, SketchPlane sketchPlane, Curve wallCurve,
            double wallHeight, double intervalInFeet)
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