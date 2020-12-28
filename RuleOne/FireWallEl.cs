
using Autodesk.Revit.DB;


namespace RuleOne
{
    public class FireWallEl
    {
        public FireWallEl(Element fireWall, Document wallDoc)
        {
            this.FireWall = fireWall;
            this.WallDoc = wallDoc;
        }

        public Element FireWall
        { get; set; }

        public Document WallDoc
        { get; set; }


    }
}
