using System;
using Autodesk.Revit.DB;

namespace Task_2.Utilities
{
    internal static class FamilyInstanceUtils
    {
        public static void VerticalHandOrientationCriterea(FamilyInstance familyCreated, Curve curveWall, XYZ familyLocationPoint, XYZ roomMidPoint, Document doc)
        {
            XYZ directionToFamily = familyLocationPoint - roomMidPoint;

            if (Math.Abs(curveWall.GetEndPoint(0).X - curveWall.GetEndPoint(1).X) < 0.001)
            {
                // Check if the wall is left or right of the room midpoint
                if (curveWall.GetEndPoint(0).X < roomMidPoint.X)
                {
                    // Wall is left of room midpoint
                    if (directionToFamily.Y < 0)
                    {
                        // Below room centroid
                        if (!familyCreated.HandOrientation.IsAlmostEqualTo(new XYZ(0, 1, 0)))
                        {
                            familyCreated.flipHand();
                        }

                        var translation = new XYZ(0, 1.5, 0);
                        ElementTransformUtils.MoveElement(doc, familyCreated.Id, translation);
                    }
                    else
                    {
                        // Above room centroid
                        if (!familyCreated.HandOrientation.IsAlmostEqualTo(new XYZ(0, -1, 0)))
                        {
                            familyCreated.flipHand();
                        }
                        var translation = new XYZ(0, -1.5, 0);
                        ElementTransformUtils.MoveElement(doc, familyCreated.Id, translation);
                    }
                }
                else
                {
                    // Wall is right of room midpoint
                    if (directionToFamily.Y < 0)
                    {
                        // Below room centroid
                        if (!familyCreated.HandOrientation.IsAlmostEqualTo(new XYZ(0, 1, 0)))
                        {
                            familyCreated.flipHand();
                        }
                        var translation = new XYZ(0, 1.5, 0);
                        ElementTransformUtils.MoveElement(doc, familyCreated.Id, translation);
                    }
                    else
                    {
                        // Above room centroid
                        if (!familyCreated.HandOrientation.IsAlmostEqualTo(new XYZ(0, -1, 0)))
                        {
                            familyCreated.flipHand();
                        }
                        var translation = new XYZ(0, -1.5, 0);
                        ElementTransformUtils.MoveElement(doc, familyCreated.Id, translation);
                    }
                }
            }

        }
        public static void HorizontalHandOrientationCriteria(FamilyInstance familyCreated, Curve curveWall, XYZ familyLocationPoint, XYZ roomMidPoint, Document doc)
        {
            XYZ directionToFamily = familyLocationPoint - roomMidPoint;

            if (Math.Abs(curveWall.GetEndPoint(0).Y - curveWall.GetEndPoint(1).Y) < 0.001)
            {
                // Check if the wall is left or right of the room midpoint
                if (familyLocationPoint.X < roomMidPoint.X)
                {
                    // Wall is left of room midpoint
                    if (directionToFamily.Y < 0)
                    {
                        // Below room centroid
                        if (!familyCreated.HandOrientation.IsAlmostEqualTo(new XYZ(1, 0, 0)))
                        {
                            familyCreated.flipHand();
                        }
                        var translation = new XYZ(1.5, 0, 0);
                        ElementTransformUtils.MoveElement(doc, familyCreated.Id, translation);
                    }
                    else
                    {
                        // Above room centroid
                        if (!familyCreated.HandOrientation.IsAlmostEqualTo(new XYZ(1, 0, 0)))
                        {
                            familyCreated.flipHand();
                        }
                        var translation = new XYZ(1.5, 0, 0);
                        ElementTransformUtils.MoveElement(doc, familyCreated.Id, translation);
                    }
                }
                else
                {
                    // Wall is right of room midpoint
                    if (directionToFamily.Y < 0)
                    {
                        // Below room centroid
                        if (!familyCreated.HandOrientation.IsAlmostEqualTo(new XYZ(-1, 0, 0)))
                        {
                            familyCreated.flipHand();
                        }
                        var translation = new XYZ(-1.5, 0, 0);
                        ElementTransformUtils.MoveElement(doc, familyCreated.Id, translation);
                    }
                    else
                    {
                        // Above room centroid
                        if (!familyCreated.HandOrientation.IsAlmostEqualTo(new XYZ(-1, 0, 0)))
                        {
                            familyCreated.flipHand();
                        }
                        var translation = new XYZ(-1.5, 0, 0);
                        ElementTransformUtils.MoveElement(doc, familyCreated.Id, translation);
                    }
                }
            }
        }
        public static void FamilyOrientationHandler(Document doc, FamilyInstance familyCreated, XYZ familyOrientation,
            Curve selectedWallCurveInRoom, XYZ familyLocationPoint, XYZ roomMidPoint)
        {
            if (!familyCreated.FacingOrientation.IsAlmostEqualTo(familyOrientation))
            {
                familyCreated.flipFacing();
            }

            if (GeometryUtilities.IsVerticalCurve(selectedWallCurveInRoom))
            {
                VerticalHandOrientationCriterea(familyCreated, selectedWallCurveInRoom, familyLocationPoint, roomMidPoint, doc);
            }
            else
            {
                HorizontalHandOrientationCriteria(familyCreated, selectedWallCurveInRoom, familyLocationPoint, roomMidPoint, doc);
            }
        }
    }
}
