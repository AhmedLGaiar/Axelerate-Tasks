using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;

namespace Task_2.Data
{
    internal class APPData
    {
        public UIApplication UIApp { get; private set; }
        public Application App { get; private set; }
        public UIDocument UIDoc { get; private set; }
        public Document Doc { get; private set; }

        private static APPData _instance;

        private APPData(UIApplication uiApp)
        {
            UIApp = uiApp;
            App = uiApp.Application;
            UIDoc = uiApp.ActiveUIDocument;
            Doc = UIDoc.Document;
        }

        public static void Initialize(UIApplication uiApp)
        {
            if (_instance == null)
            {
                _instance = new APPData(uiApp);
            }
        }

        public static APPData Current
        {
            get
            {
                if (_instance == null)
                    throw new System.InvalidOperationException("APPData has not been initialized. Call Initialize() first.");
                return _instance;
            }
        }
    }
}