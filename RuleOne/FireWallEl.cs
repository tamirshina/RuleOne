using System;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Text;

namespace RuleOne
{
    public class FireWallEl
    {
        public FireWallEl(Element fireWall, Document wallDoc)
        {
            FireWall = fireWall;
            WallDoc = wallDoc;
        }

        public Element FireWall
        { get; set; }

        public Document WallDoc
        { get; set; }


    }
}
