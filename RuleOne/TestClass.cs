

namespace RuleOne
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autodesk.Revit.Attributes;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.UI;
    using static RuleOne.Helper;
    using static RuleOne.Lists;
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCalss : IExternalCommand
    {
        public Result Execute(ExternalCommandData revit, ref string message, ElementSet elements)
        {

            UIDocument uiDoc = revit.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;



            //SeeNoBBEl(uiDoc, doc);

            return Result.Succeeded;
        }
    }



}
