

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

            SeeNoBBEl(uiDoc, doc);

            return Result.Succeeded;
        }
        public void SeeNoBBEl(UIDocument uiDoc, Document doc)
        {
            List<Element> noBBList =  GetNoBBInHost(doc);
            SelectIds(uiDoc, doc, GetIdsFromEls(noBBList));

        }
        public List<Element> GetNoBBInHost( Document doc)
        {
            List<Element> hostDucts = new List<Element>();
            ElementMulticategoryFilter ductFilter = new ElementMulticategoryFilter(customBuiltInCats);

            foreach (Element el in new FilteredElementCollector(doc)
            .WherePasses(ductFilter))
            {
                try
                {
                    if (IsHrizontal(el))
                    {
                        var bb = el.get_BoundingBox(null);
                        hostDucts.Add(el);
                        if (bb == null)
                        {
                           
                            bbIsNull.Add(el);
                        }
                    }
                }
                catch (Exception exc)
                {
                    ExceptionFound.Add(exc.ToString());
                }
            }
            return hostDucts;
        }
        public void SelectIds(UIDocument uiDoc, Document doc,  ICollection<ElementId> idList)
        {
            try
            {
                PrintResultsHaseSet("no BB", bbIsNull);
                var view = uiDoc.ActiveView as View3D;
                using var tran = new Transaction(doc, "Test");
                tran.Start();
                view.HideElements(idList);
                tran.Commit();
            }
            catch (Exception e)
            { TaskDialog.Show("revit", e.Message); }

        }
        public ICollection<ElementId> GetIdsFromEls(List<Element> elList)
        {
            ICollection<ElementId> listOfIds = new List<ElementId>();
            foreach (Element el in elList)
            {
                listOfIds.Add(el.Id);
            }
            return listOfIds;
        }
    }



}
