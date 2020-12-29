
using Autodesk.Revit.DB;

namespace RuleOne
{
    public class ResultType
    {
        public ResultType(Element fireWallOrMetalBeam, Element duct)
        {
            this.FireWallOrMetalBeam = fireWallOrMetalBeam;
            this.DuctElement = duct;
        }

        public Element FireWallOrMetalBeam
        { get; set; }

        public Element DuctElement
        { get; set; }

    }
}
