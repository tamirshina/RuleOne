using System;

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
    public class VerifyFDInPlace : IExternalCommand
    {
        public Result Execute(ExternalCommandData revit, ref string message, ElementSet elements)
        {

			UIDocument uiDoc = revit.Application.ActiveUIDocument;
			Document doc = uiDoc.Document;

			FinalFunc(doc);

			return Result.Succeeded;
        }
		public void FinalFunc(Document doc)
		{
			List<Element> wallsList = new List<Element>();

			Transform mepTransform = getTransform(doc, "MEP");
			Transform arcTransform = getTransform(doc, "ARC");
			Transform strTransform = getTransform(doc, "STR");
			
			Document arcDoc = getDocument(doc, "ARC");
			Document strDoc = getDocument(doc, "STR");
			Document mepDoc = getDocument(doc, "MEP");

			List<Element> ductList = GetHorizontalDuctsInLinked(doc, mepDoc);
			foreach (Element e in ductList)
			{
				//get ARC intersections 
				List<Element> tempsList = GetIntesectingWalls(e, arcDoc, arcTransform, mepTransform, doc);
				foreach (Element el in tempsList)
				{
					wallsList.Add(el);
				}
				//get STR intersections 
				List<Element> tempsListStr = GetIntesectingWalls(e, strDoc, strTransform, mepTransform, doc);
				foreach (Element el in tempsList)
				{
					wallsList.Add(el);
				}
			}
			PrintResults("fireDumperIntersects", fireDumpers);
			PrintResults("whereIsFD", whereIsFD);
			PrintResults("no fam name", noFam);
			PrintExceptions();
			ClearLists();
		}
		public List<Element> GetHorizontalDuctsInLinked(Document doc, Document linkedDoc)
		{
			List<Element> linkedDucts = new List<Element>();

			ElementMulticategoryFilter ductFilter = new ElementMulticategoryFilter(ductsBuiltInCats);

			foreach (Element linkedEl in new FilteredElementCollector(linkedDoc)
			.WherePasses(ductFilter))
			{
				try
				{
					if (isHrizontal(linkedEl))
					{
						linkedDucts.Add(linkedEl);
					}
				}
				catch (Exception exc)
				{
					ExceptionFound.Add(exc.ToString());
				}
			}
			return linkedDucts;
		}
		private List<Element> GetIntesectingWalls(Element elToIntersect, Document targetDoc,Transform targetTransform,
			Transform sourceTransform, Document doc)
		{
			List<Element> wallsList = new List<Element>();
			try
			{
				var bb = elToIntersect.get_BoundingBox(doc.ActiveView);

				if (bb != null)
				{
					var filter = new BoundingBoxIntersectsFilter(new Outline(TransformPoint(bb.Min, sourceTransform, targetTransform),
																		 TransformPoint(bb.Max, sourceTransform, targetTransform)));

					FilteredElementCollector collector = new FilteredElementCollector(targetDoc);
					ElementCategoryFilter willFil = new ElementCategoryFilter(BuiltInCategory.OST_Walls);

					List<Element> intersectsList = collector.WherePasses(filter).WherePasses(willFil).ToList();

					foreach (Element e in intersectsList)
					{
						if (AssertFrireWall(e))
						{
							if (AssertFireDumper(elToIntersect))
							{
								fireDumpers.Add(elToIntersect);
							}
							else
							{
								findIntersectionPoint(doc, elToIntersect, e, sourceTransform, targetTransform);
							}
						}									
					}
				}else
				{
					bbIsNull.Add(elToIntersect);
				}
			}
			catch (Exception exc)
			{
				Exception ex = exc;
			}
			return wallsList;
		}
		public void findIntersectionPoint(Document doc, Element ductEl, Element wallEl, Transform mepModel, Transform wallDoc)
		{
			Wall wall = wallEl as Wall;
			List<Face> wallNormalFaces = FindWallNormalFace(wall);
            bool isFD = false;

			try
			{
				foreach (Face wallFace in wallNormalFaces)
				{
					var intersectionSolid = BooleanOperationsUtils.ExecuteBooleanOperation(TurnWallFaceToSolid(wallFace),
						TurnElToSolid(ductEl, mepModel), BooleanOperationsType.Intersect);//check why is the exception.

					if (intersectionSolid.Volume > 0)
					{
						PlanarFace solidPlanarFace = getIntersectionSolidRightFace(intersectionSolid, wallFace);

                        Solid finalSolid = CreateSolidFromVertices((double)(8 / 12 + wall.Width), getVerticesFromPlanarFace(solidPlanarFace),
							solidPlanarFace.FaceNormal.Negate());
                        //PaintSolid(doc, finalSolid, 1);
                        FilteredElementCollector collector = new FilteredElementCollector(getDocument(doc, "MEP"));

						collector.WherePasses(new ElementIntersectsSolidFilter(finalSolid));

						foreach (Element element in collector)
						{
							if (AssertFireDumper(element))
							{
								isFD = true;
							}
						}
						if (!isFD)
						{
							whereIsFD.Add(wallEl);
						}
						break;
					}
				}
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
			}
		}


	}
}