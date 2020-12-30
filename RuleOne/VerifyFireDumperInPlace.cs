using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RuleOne
{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class VerifyFireDumperInPlace : IExternalCommand
    {
		public Result Execute(ExternalCommandData revit, ref string message, ElementSet elements)
        {

			UIDocument uiDoc = revit.Application.ActiveUIDocument;
			Document activeDocument = uiDoc.Document;

			MainExecution(activeDocument);

			return Result.Succeeded;
        }
		private void MainExecution(Document activeDocument)
		{
            List <Model> allModels  = GetAllModels(activeDocument);
			List<Element> ducts = ExecutionHelper.GetHorizontalDucts(allModels.Where(m => m.isMep));

			List<Element> whereIsFireDumper = IntersectionHelper.CheckIsMissingFireDumper(activeDocument, ducts.Where(d => !IdentificationHelper.IsFireDumper(d)), allModels);

			PrintResults("whereIsFD", whereIsFireDumper);
			PrintExceptions();
			ClearConstants(); 		
        }
		private List<Model> GetAllModels(Document activeDocument)
        {
			List<Model> models = new List<Model>();
			 ;
			foreach (var m in new FilteredElementCollector(activeDocument).OfClass(typeof(RevitLinkInstance)))
			{
				var linkedModel = ((RevitLinkInstance)m);
				models.Add(new Model(linkedModel.GetLinkDocument(), linkedModel.GetTotalTransform(),
					linkedModel.GetLinkDocument().Title.Contains("MEP") ? true : false));
			}
			//Add host 
			models.Add(new Model(activeDocument, Transform.Identity, false));
			return models;
			throw new NotImplementedException();
        }
		private static void PrintExceptions()
		{
			string exInfo = "";
			foreach (string str in Constants.ExceptionFound)
			{
				exInfo += str + " " + Environment.NewLine + Environment.NewLine;
			}
			TaskDialog.Show("revit", "count: " + Constants.ExceptionFound.Count() + Environment.NewLine + exInfo);
		}
		private static void ClearConstants()
		{
			Constants.ExceptionFound.Clear();
		}
		private static void PrintResults(string headline, List<Element> elList)
		{
			string info = "";
			foreach (Element duct in elList)
			{
				if (duct != null)
				{
					string name = duct.Name;
					info += name + " " + duct.Id + "  " + "catagory" + " " + (BuiltInCategory)duct.Category.Id.IntegerValue + Environment.NewLine;
				}

			}
			TaskDialog.Show("revit", "Count: " + elList.Count() + Environment.NewLine + headline + "-"
							+ Environment.NewLine + info + Environment.NewLine);
		}

	}
}