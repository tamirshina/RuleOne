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

            Transform mepTransform = GetTransform(doc, "MEP");

            Document mepDoc = GetDocument(doc, "MEP");

            List<Element> ductList = GetHorizontalDuctsInLinked(mepDoc);
            foreach (Element e in ductList)
            {
				GetIntesectingWalls(e, GetAllLinked(doc), mepTransform, doc);
            }
			PrintResults("fireDumperIntersects", fireDumpers);
            PrintResults("whereIsFD", whereIsFD);
			PrintExceptions();
            ClearLists();
        }
		public List<Element> GetIntesectingWalls(Element elToIntersect, List<RevitLinkInstance> targetDocs, Transform mepTransform,
			Document doc)
		{
			List<Element> wallsList = new List<Element>();

			foreach (RevitLinkInstance linkedInstance in targetDocs)
			{
				try
				{
					var bb = elToIntersect.get_BoundingBox(doc.ActiveView);

					if (bb != null)
					{
						var filter = new BoundingBoxIntersectsFilter(new Outline(TransformPoint(bb.Min, mepTransform, linkedInstance.GetTotalTransform()),
																			 TransformPoint(bb.Max, mepTransform, linkedInstance.GetTotalTransform())));

						FilteredElementCollector collector = new FilteredElementCollector(linkedInstance.GetLinkDocument());

						List<Element> intersectsList = collector.WherePasses(filter).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_Walls)).ToList();

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
									FindIntersectionPoint(doc, elToIntersect, e, mepTransform, linkedInstance);
								}
							}
						}
					}
					else
					{
						bbIsNull.Add(elToIntersect);
					}
				}
				catch (Exception exc)
				{
					Exception ex = exc;
				}
			}
			return wallsList;
		}
		public void FindIntersectionPoint(Document doc, Element ductEl, Element wallEl, Transform mepTransform, RevitLinkInstance linkedInstance)
		{
			Wall wall = wallEl as Wall;
			List<Face> wallNormalFaces = FindWallNormalFace(wall);
			bool isFD = false;

			try
			{
				foreach (Face wallFace in wallNormalFaces)
				{

					var intersectionSolid = BooleanOperationsUtils.ExecuteBooleanOperation(TurnWallFaceToSolid(wallFace, linkedInstance),
						TurnElToSolid(ductEl, mepTransform), BooleanOperationsType.Intersect);//check why is the exception.

					if (intersectionSolid.Volume > 0)
					{
						PlanarFace solidPlanarFace = getIntersectionSolidRightFace(intersectionSolid, wallFace);

						Solid finalSolid = CreateSolidFromVertices((double)(8 / 12 + wall.Width), getVerticesFromPlanarFace(solidPlanarFace),
							solidPlanarFace.FaceNormal.Negate());
						Solid finalSolidInStrcModel = TransformSolid(linkedInstance.GetTransform(), Transform.Identity, finalSolid);
						//PaintSolid(doc, finalSolid, 1);
						FilteredElementCollector collector = new FilteredElementCollector(GetDocument(doc, "MEP"));

						collector.WherePasses(new ElementIntersectsSolidFilter(finalSolidInStrcModel));

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
		public List<Element> GetHorizontalDuctsInLinked(Document linkedDoc)
		{
			List<Element> linkedDucts = new List<Element>();

			ElementMulticategoryFilter ductFilter = new ElementMulticategoryFilter(ductsBuiltInCats);

			foreach (Element linkedEl in new FilteredElementCollector(linkedDoc)
			.WherePasses(ductFilter))
			{
				try
				{
					if (IsHrizontal(linkedEl))
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
		


	}
}