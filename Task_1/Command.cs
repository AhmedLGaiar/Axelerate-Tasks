using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Task_1.Utilities;
using Task_1.Helpers;

namespace Task_1
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            try
            {
                FloorType floorType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .OfCategory(BuiltInCategory.OST_Floors)
                    .OfType<FloorType>()
                    .FirstOrDefault();

                Level level = doc.ActiveView.GenLevel;

                List<Line> userInput = UserInput.GetLines();

                if (level == null)
                {
                    message = "There is No level found in the active view.";
                    return Result.Failed;
                }

                if (floorType == null)
                {
                    message = "There is No floor type found in the project.";
                    return Result.Failed;
                }

                CurveLoop loop;

                if (!FloorLoopHelper.TryCreateValidFloorLoop(userInput, out loop))
                {
                    if (!FloorLoopHelper.TrySortLinesToLoop(userInput, out List<Line> sortedLines) ||
                        !FloorLoopHelper.TryCreateValidFloorLoop(sortedLines, out loop))
                    {
                        TaskDialog.Show("Error", "The lines do not form a valid closed loop.");
                        return Result.Failed;
                    }
                }

                using (Transaction tx = new Transaction(doc, "Create Floor"))
                {
                    tx.Start();
                    Floor.Create(doc, new List<CurveLoop> { loop }, floorType.Id, level.Id);
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