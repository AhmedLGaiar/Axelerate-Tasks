using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using Task_2.Filters;
using Task_4.Data;
using Task_4.Utilities;

namespace Task_4
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
                // Pick a wall
                Reference pickedRef =
                    uidoc.Selection.PickObject(ObjectType.Element, new WallSelectionFilter(), "Pick a wall");
                Wall wall = doc.GetElement(pickedRef) as Wall;

                // Get wall solid
                Solid solid = FrameUtilities.GetWallSolid(wall);
                if (solid == null)
                {
                    TaskDialog.Show("Error", "Couldn't extract wall solid.");
                    return Result.Failed;
                }

                // Get wall face
                Face wallFace = solid.Faces.Cast<Face>().OrderByDescending(f => f.Area).FirstOrDefault();
                if (wallFace == null)
                    return Result.Failed;

                XYZ faceNormal = wallFace.ComputeNormal(new UV(0.5, 0.5));
                IList<CurveLoop> loops = wallFace.GetEdgesAsCurveLoops();

                using (Transaction tx = new Transaction(doc, "Wall Framing"))
                {
                    tx.Start();

                    FrameUtilities.FrameWall(doc, wall, solid, wallFace, faceNormal, loops);

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