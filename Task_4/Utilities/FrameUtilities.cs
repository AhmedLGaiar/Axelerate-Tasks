using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Task_4.Utilities
{
    internal static class FrameUtilities
    {
        static double studThickness = UnitUtils.ConvertToInternalUnits(0.15, UnitTypeId.Meters); // ~15 cm
        static double studSpacing = UnitUtils.ConvertToInternalUnits(2.0, UnitTypeId.Feet);       // 2 feet
        internal static void FrameWall(Document doc, Wall wall, Solid solid, Face face, XYZ normal, IList<CurveLoop> loops)
        {
            LocationCurve lc = wall.Location as LocationCurve;
            Curve wallCurve = lc.Curve;
            XYZ wallDir = (wallCurve.GetEndPoint(1) - wallCurve.GetEndPoint(0)).Normalize();
            double wallWidth = wall.Width;

            // Vertical studs
            int count = (int)(wallCurve.Length / studSpacing);
            for (int i = 1; i < count; i++)
            {
                double param = i * studSpacing / wallCurve.Length;
                XYZ mid = wallCurve.Evaluate(param, true);
                PlaceVerticalStud(doc, solid, mid, normal, wallDir, wallWidth);
            }

            // Top and bottom plates
            Transform sideOffset = Transform.CreateTranslation(normal * wallWidth * 0.5);
            Curve wallOnFace = wallCurve.CreateTransformed(sideOffset);

            CreateDoubleLine(doc, wallOnFace, normal, XYZ.BasisZ * studThickness); // bottom plate
            CreateDoubleLine(doc, wallOnFace.CreateTransformed(Transform.CreateTranslation(XYZ.BasisZ * wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble())), normal, XYZ.BasisZ * studThickness); // top plate

            // Outer studs
            foreach (Curve c in loops[0])
            {
                if (!IsBottomEdge(c)) CreateDoubleLine(doc, c, normal, normal.CrossProduct((c.GetEndPoint(1) - c.GetEndPoint(0)).Normalize()) * studThickness);
            }

            // Openings (door/window)
            var openings = GetWallOpenings(doc, wall);
            for (int i = 1; i < loops.Count; i++)
            {
                bool isDoor = openings.Any(o => o.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors);
                FrameOpening(doc, loops[i], normal, isDoor);
            }
        }

        private static void PlaceVerticalStud(Document doc, Solid solid, XYZ point, XYZ normal, XYZ dir, double width)
        {
            XYZ bottom = point - XYZ.BasisZ * 100;
            XYZ top = point + XYZ.BasisZ * 100;
            Line line = Line.CreateBound(bottom, top);

            var intersect = solid.IntersectWithCurve(line, new SolidCurveIntersectionOptions());
            for (int i = 0; i < intersect.SegmentCount; i++)
            {
                Curve seg = intersect.GetCurveSegment(i);
                if (seg == null || seg.Length < studThickness * 2) continue;

                XYZ start = seg.GetEndPoint(0) + (seg.GetEndPoint(1) - seg.GetEndPoint(0)).Normalize() * studThickness;
                XYZ end = seg.GetEndPoint(1) - (seg.GetEndPoint(1) - seg.GetEndPoint(0)).Normalize() * studThickness;
                Curve trimmed = Line.CreateBound(start, end);

                Curve moved = trimmed.CreateTransformed(Transform.CreateTranslation(normal * width * 0.5));
                CreateDoubleLine(doc, moved, normal, dir * studThickness * 0.5);
            }
        }

        private static void FrameOpening(Document doc, CurveLoop loop, XYZ normal, bool isDoor)
        {
            List<Curve> curves = loop.ToList();
            int bottomIdx = -1;
            double minZ = double.MaxValue;

            for (int i = 0; i < curves.Count; i++)
            {
                var c = curves[i];
                double avgZ = (c.GetEndPoint(0).Z + c.GetEndPoint(1).Z) / 2;
                if (avgZ < minZ) { minZ = avgZ; bottomIdx = i; }
            }

            for (int i = 0; i < curves.Count; i++)
            {
                if (isDoor && i == bottomIdx) continue;
                if (IsBottomEdge(curves[i])) continue;

                XYZ dir = (curves[i].GetEndPoint(1) - curves[i].GetEndPoint(0)).Normalize();
                XYZ offset = normal.CrossProduct(dir) * studThickness;
                CreateDoubleLine(doc, curves[i], normal, offset);
            }
        }

        private static void CreateDoubleLine(Document doc, Curve baseCurve, XYZ normal, XYZ offset)
        {
            CreateLine(doc, baseCurve, normal);
            CreateLine(doc, baseCurve.CreateTransformed(Transform.CreateTranslation(offset)), normal);
        }

        private static void CreateLine(Document doc, Curve c, XYZ normal)
        {
            Plane p = Plane.CreateByNormalAndOrigin(normal, c.GetEndPoint(0));
            SketchPlane sp = SketchPlane.Create(doc, p);
            doc.Create.NewModelCurve(c, sp);
        }

        internal static Solid GetWallSolid(Wall wall)
        {
            GeometryElement geo = wall.get_Geometry(new Options() { ComputeReferences = true });
            return geo.OfType<Solid>().FirstOrDefault(s => s.Volume > 0);
        }

        private static List<FamilyInstance> GetWallOpenings(Document doc, Wall wall)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Host != null && fi.Host.Id == wall.Id)
                .ToList();
        }

        private static bool IsBottomEdge(Curve c)
        {
            double z1 = c.GetEndPoint(0).Z, z2 = c.GetEndPoint(1).Z;
            return Math.Abs(z1 - z2) < 1e-3 && (z1 + z2) / 2 < 1.0;
        }

        private class WallFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Wall;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
