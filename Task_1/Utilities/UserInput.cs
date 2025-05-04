using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Task_1.Utilities
{
    internal class UserInput
    {
        public static List<Line> GetLines()
        {
            return new List<Line>
            {
                Line.CreateBound(new XYZ(0, 0, 0), new XYZ(79, 0, 0)),
                Line.CreateBound(new XYZ(44, 25, 0), new XYZ(13, 25, 0)),
                Line.CreateBound(new XYZ(13, 40, 0), new XYZ(-8, 40, 0)),
                Line.CreateBound(new XYZ(55, 34, 0), new XYZ(55, 10, 0)),
                Line.CreateBound(new XYZ(79, 34, 0), new XYZ(55, 34, 0)),
                Line.CreateBound(new XYZ(0, 20, 0), new XYZ(0, 0, 0)),
                Line.CreateBound(new XYZ(55, 10, 0), new XYZ(44, 12, 0)),
                Line.CreateBound(new XYZ(-8, 40, 0), new XYZ(-8, 20, 0)),
                Line.CreateBound(new XYZ(79, 0, 0), new XYZ(79, 34, 0)),
                Line.CreateBound(new XYZ(44, 12, 0), new XYZ(44, 25, 0)),
                Line.CreateBound(new XYZ(-8, 20, 0), new XYZ(0, 20, 0)),
                Line.CreateBound(new XYZ(13, 25, 0), new XYZ(13, 40, 0))
            };
        }
    }
}