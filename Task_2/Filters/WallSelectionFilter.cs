using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Task_2.Filters
{
    internal class WallSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Wall;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }

}
