using System;
using System.Collections.Generic;
using System.Text;

namespace RuleOne
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autodesk.Revit.Attributes;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.DB.Mechanical;
    using Autodesk.Revit.UI;
    using static RuleOne.Helper;
    using static RuleOne.Lists;
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]

    public class SecondStep : IExternalCommand
    {
        public Result Execute(ExternalCommandData revit, ref string message, ElementSet elements)
        {

            UIDocument uiDoc = revit.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;


            return Result.Succeeded;
        }
		public bool IsPassingThrough(Document doc, Element element)
		{
			try
			{
				Duct duct = element as Duct;

				if (HasInsolation(duct) || HasOpening(doc, duct))
				{
					return false;
				}
				else
				{
					if (CheckAdjacentDucts(doc, duct))
					{
						return true;
					}
					else
					{
						//MoveToNext();
					}
				}
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
			}

			return false;
		}

		private bool CheckAdjacentDucts(Document doc, Duct duct)
		{
			foreach (Connector con in duct.ConnectorManager.Connectors)
			{
				foreach (Connector co in con.AllRefs)
				{
					Duct nextDuct = co.Owner as Duct;

					if (HasInsolation(nextDuct) && !HasOpening(doc, nextDuct))
					{
						if (IsEndWithExteriorWallOrFireWall(doc, nextDuct))
						{
							return true;
						}
					}
				}
			}
			return false;
		}

        private bool HasOpening(Document doc, Duct duct)
        {
			if(GetIntesectingElementsByCatagory(doc, duct, BuiltInCategory.OST_Walls).Any())
			{
				return true;
			}
			return false;
		}
        private bool IsEndWithExteriorWallOrFireWall(Document doc, Element element)
		{
			foreach (Element el in GetIntesectingElementsByCatagory(doc, element, BuiltInCategory.OST_Walls))
			{
				Wall wall = el as Wall;
				if (wall.WallType.Function.ToString() == "Exterior" || AssertFrireWall(wall))
				{
					return true;
				}
			}

			return false;
		}
		private bool IsDuctFiting(Element owner)
		{
			{
				BuiltInCategory bipFraming = BuiltInCategory.OST_DuctFitting;
				BuiltInCategory elCat = (BuiltInCategory)owner.Category.Id.IntegerValue;
				if (bipFraming.Equals(elCat))
				{
					return true;
				}
				return false;
			}
		}
		private bool HasInsolation(Duct duct)
		{
			Parameter insulationThickness = duct.get_Parameter(BuiltInParameter.RBS_REFERENCE_INSULATION_THICKNESS);

			if (insulationThickness.AsDouble() > 0)
			{
				return true;
			}
			return false;
		}
    }
}
