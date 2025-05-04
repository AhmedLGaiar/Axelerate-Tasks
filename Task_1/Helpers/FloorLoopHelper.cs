using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Task_1.Helpers
{
    /// <summary>
    /// Provides utility methods for validating and organizing a set of lines to form a valid Revit floor boundary loop.
    /// </summary>
    internal class FloorLoopHelper
    {
        /// <summary>
        /// Validates whether a given list of lines forms a valid closed and connected loop,
        /// and attempts to create a <see cref="CurveLoop"/> from them if so.
        /// </summary>
        /// <param name="lines">The list of <see cref="Line"/> elements to validate.</param>
        /// <param name="loop">The resulting <see cref="CurveLoop"/> if valid; otherwise null.</param>
        /// <returns>True if a valid loop is formed; otherwise false.</returns>
        public static bool TryCreateValidFloorLoop(List<Line> lines, out CurveLoop loop)
        {
            loop = null;

            // Must have at least 3 lines to form a closed loop
            if (lines.Count < 3) return false;

            // Ensure each line connects to the next
            for (int i = 0; i < lines.Count; i++)
            {
                XYZ end = lines[i].GetEndPoint(1); // End of current line
                XYZ nextStart = lines[(i + 1) % lines.Count].GetEndPoint(0); // Start of the next line
                if (!end.IsAlmostEqualTo(nextStart))
                    return false;
            }

            // try to Create CurveLoop
            try
            {
                loop = new CurveLoop();
                foreach (var line in lines)
                    loop.Append(line);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to reorder an unordered list of lines into a sorted list that forms a closed, connected loop.
        /// </summary>
        /// <param name="lines">The unordered list of <see cref="Line"/> objects.</param>
        /// <param name="sorted">The output sorted list forming a closed loop if successful.</param>
        /// <returns>True if the lines could be sorted into a closed loop; otherwise false.</returns>
        public static bool TrySortLinesToLoop(List<Line> lines, out List<Line> sorted)
        {
            sorted = new List<Line>();
            var unused = new List<Line>(lines); 

            // Start the loop with the first line
            sorted.Add(unused[0]);      
            unused.RemoveAt(0);        

            while (unused.Count > 0)
            {
                // Get the end point of the last line in the sorted list
                var lastEnd = sorted.Last().GetEndPoint(1); 

                // Find the next line that connects to the last end point
                var next = unused.FirstOrDefault(l =>
                    l.GetEndPoint(0).IsAlmostEqualTo(lastEnd) ||   // line  it starts or ends at lastEnd
                    l.GetEndPoint(1).IsAlmostEqualTo(lastEnd));    

                if (next == null)
                    return false; // No connecting line found → cannot form a loop

                // If next line is reversed, flip it so it starts at lastEnd
                if (!next.GetEndPoint(0).IsAlmostEqualTo(lastEnd))
                    next = (Line)next.CreateReversed();

                sorted.Add(next);      
                unused.Remove(next);   // Remove it to unused list so we don't use it again
            }

            // Final check: does the last line connect back to the start of the first line?
            if (!sorted.Last().GetEndPoint(1).IsAlmostEqualTo(sorted.First().GetEndPoint(0)))
                return false;

            return true; // Successfully built a closed, connected loop
        }
    }
}