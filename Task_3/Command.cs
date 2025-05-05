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

                        // We'll use the first boundary loop (rooms can have inner loops/holes)
                        CurveLoop outerLoop = new CurveLoop();
                        foreach (BoundarySegment segment in boundaries[0])
                        {
                            outerLoop.Append(segment.GetCurve());
                        }

                        List<CurveLoop> loops = new List<CurveLoop> { outerLoop };
                        Floor.Create(doc, loops, genericFloor.Id, level.Id);

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