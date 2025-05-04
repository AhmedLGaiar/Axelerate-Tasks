using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB.Architecture;
using Task_2.Filters;
using Task_2.Utilities;
using Autodesk.Revit.DB.Structure;
using Task_2.Data;

namespace Task_2
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
                // force user to picks a wall only
                WallSelectionFilter wallFilter = new WallSelectionFilter();
                Reference pickedRef =
                    uidoc.Selection.PickObject(ObjectType.Element, wallFilter, "Pick a bathroom wall");
                Wall pickedWall = doc.GetElement(pickedRef) as Wall;

                FamilySymbol wcFamilySymbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(f => f.Name == "ADA");

                if (wcFamilySymbol == null)
                {
                    TaskDialog.Show("Warning", "FamilySymbol 'ADA' was not found in the project.");
                    return Result.Failed;
                }

                Room bathrooms = RoomUtilities.GetBathroomsAttachedToWall(doc, pickedWall,
                    out List<BoundarySegment> boundarySegmentList);
                if (bathrooms is null)
                {
                    TaskDialog.Show("Info", "No bathroom found attached to this wall.");
                    return Result.Failed;
                }

                Curve selectedCurveInRoom = RoomUtilities.GetWallCurveInsideRoom(boundarySegmentList, pickedWall);

                FamilyInstance door = RoomUtilities.FindDoorInRoom(doc, bathrooms);
                if (door == null)
                {
                    TaskDialog.Show("Info", "No door found in the bathroom.");
                    return Result.Failed;
                }

                // Get door location point
                LocationPoint doorLocation = door.Location as LocationPoint;
                if (doorLocation == null)
                {
                    TaskDialog.Show("Error", "Failed to get door location.");
                    return Result.Failed;
                }

                XYZ doorPoint = doorLocation.Point;

                XYZ wcLocationPoint = GeometryUtilities.FarthestPointFromDoor(doorPoint, selectedCurveInRoom);

                using (Transaction tx = new Transaction(doc, TransactionName.PlaceBathRooms))
                {
                    tx.Start();
                    if (!wcFamilySymbol.IsActive)
                        wcFamilySymbol.Activate();

                    // Get best orientation (away from door)
                    XYZ orientation = GeometryUtilities.GetBestOrientation(pickedWall, wcLocationPoint, doorPoint);

                    // Place WC inside bathroom, aligned and facing away from door
                    doc.Create.NewFamilyInstance(wcLocationPoint, wcFamilySymbol, orientation, bathrooms, StructuralType.NonStructural);

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