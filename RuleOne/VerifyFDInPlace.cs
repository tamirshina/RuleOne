using System;

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

			List<Element> ductList = GetHorizontalDuctsInLinked(GetDocument(doc, "MEP"));

			foreach (Element ductEl in ductList)
			{
				try
				{
					if (!AssertFireDumper(ductEl))
					{
						foreach (Element ele in GetIntesectingElements(doc, ductEl))
						{
							if (AssertFrireWall(ele))
							{
								if (!IsFireDumperInIntersection(doc, ductEl, ele))
								{
									if (ElementIsNotInTheList(ele))
									{
										whereIsFD.Add(ele);
									}
								}
							}
							if (AssertMetalBeam(ele))
							{
								if (IsMetalBeamIntersectsFW	(doc, ele))
								{
									if (!IsFireDumperIntersectDuctSolid(doc, ductEl))
									{
										if (ElementIsNotInTheList(ele))
										{
											whereIsFD.Add(ele);
										}
									}
								}								
							}							
						}
					}
					else
					{
						fireDumpers.Add(ductEl);
					}
				}
				catch (Exception exc)
				{
					ExceptionFound.Add(exc.ToString());
				}
			}
				PrintResults("whereIsFD", whereIsFD);
				PrintExceptions();

				ClearLists(); 		
        }

        private bool ElementIsNotInTheList(Element ele)
        {
			foreach (ElementId id in GetIdsFromEls(whereIsFD))
			{
				if (ele.Id.Equals(id))
				{
					return false;
				}
			}
			return true;		
		}

        private bool IsFireDumperIntersectDuctSolid(Document doc, Element ductEl)
        {
			try
			{
				Solid ductSolid = TurnElToSolid(ductEl, GetTransform(doc, ductEl.Document.Title));
				Solid ductScaled = ScaleSolidInPlace(ductSolid, (double)1.25);
				
				foreach (Element ele in GetIntesectingElements(doc, ductSolid))
				{
					if (AssertFireDumper(ele))
					{
						return true;
					}
				}
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
			}
			return false;
        }
        public HashSet<Element> GetIntesectingElements(Document doc, Element elToIntersect)
		{
			HashSet<Element> elementList = new HashSet<Element>();

			foreach (RevitLinkInstance linkedInstance in GetAllLinked(doc))
			{
				try
				{
					var bb = elToIntersect.get_BoundingBox(null);

					if (bb != null)
					{
						var filter = new BoundingBoxIntersectsFilter(new Outline(TransformPoint(bb.Min, GetTransform(doc,elToIntersect.Document.Title),
							linkedInstance.GetTotalTransform()),TransformPoint(bb.Max, GetTransform(doc, elToIntersect.Document.Title),
							linkedInstance.GetTotalTransform())));

						FilteredElementCollector collector = new FilteredElementCollector(linkedInstance.GetLinkDocument());

						List<Element> intersectsList = collector.WherePasses(filter).WherePasses(new ElementMulticategoryFilter(filterBuiltInCats)).ToList();

						foreach (Element intersectionEl in intersectsList)
						{
							elementList.Add(intersectionEl);
						}
					}
				}
				catch (Exception exc)
				{
					ExceptionFound.Add(exc.ToString());
				}
			}
			return elementList;
		}
		public HashSet<Element> GetIntesectingElements(Document doc, Solid solidToIntersect)
		{
			HashSet<Element> elementList = new HashSet<Element>();

			foreach (RevitLinkInstance linkedInstance in GetAllLinked(doc))
			{
				try
				{
					Transform transform = linkedInstance.GetTransform();
					if (!transform.AlmostEqual(Transform.Identity))
					{
						solidToIntersect = SolidUtils.CreateTransformed(
						  solidToIntersect, transform.Inverse);
					}
					FilteredElementCollector collector = new FilteredElementCollector(linkedInstance.GetLinkDocument());

					List<Element> intersectsList = collector.WherePasses(new ElementIntersectsSolidFilter(solidToIntersect)).ToList();

					foreach (Element intersectionEl in intersectsList)
					{
						elementList.Add(intersectionEl);
					}
					
				}
				catch (Exception exc)
				{
					ExceptionFound.Add(exc.ToString());
				}
			}
			return elementList;
		}
		public bool IsMetalBeamIntersectsFW(Document doc, Element strucualBeam)
		{
			try
			{
				Solid beamSolid = TurnElToSolid(strucualBeam, GetTransform(doc, strucualBeam.Document.Title));
				//PaintSolid(doc, beamSolid, 1);
				PlanarFace face = GetBottomFaceOfSolid(doc, beamSolid);
				List<XYZ> vertices = getVerticesFromPlanarFace(face, 1);
				Solid faceSolid = CreateSolidFromVertices((double)1 / 3, vertices, -XYZ.BasisZ);

				foreach (Element ele in GetIntesectingElements(doc, faceSolid))
				{
					if (AssertFrireWall(ele))
					{
						var intersectionSolid = BooleanOperationsUtils.ExecuteBooleanOperation(faceSolid, TurnElToSolid(ele, GetTransform(doc, ele.Document.Title)), BooleanOperationsType.Intersect);
						if (intersectionSolid.Volume > 0)
						{
							return true;
						}						
						return true;
					}
				}
                return false;
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
				return false;
			}
		}
		public PlanarFace GetBottomFaceOfSolid(Document doc, Solid solid)
		{
			PlanarFace face =null;
			foreach (Face geomFace in solid.Faces)
			{
				if (geomFace is PlanarFace planar)
				{
					if (planar.FaceNormal.IsAlmostEqualTo(-XYZ.BasisZ))
					{
						face = planar;
					}
				}
			}
			return face;

		}
		public bool IsFireDumperInIntersection(Document doc, Element ductEl, Element wallEl)
		{
			Transform mepTransform = GetTransform(doc, ductEl.Document.Title);
			RevitLinkInstance linkedInstance = GetRevitLinkedInstance(doc, wallEl.Document.Title); 
			Wall wall = wallEl as Wall;
			List<Face> wallNormalFaces = FindWallNormalFace(wall);
			bool isFD = false;

			try
			{
				foreach (Face wallFace in wallNormalFaces)
				{

					var intersectionSolid = BooleanOperationsUtils.ExecuteBooleanOperation(TurnWallFaceToSolid(wallFace, linkedInstance),
						TurnElToSolid(ductEl, mepTransform), BooleanOperationsType.Intersect);

					if (intersectionSolid.Volume > 0)
					{
						if (ductEl.Id.ToString() == "135902")
						{
							string shina = "any";
						}
						PlanarFace solidPlanarFace = getIntersectionSolidRightFace(intersectionSolid, wallFace);

						Solid finalSolid = CreateSolidFromVertices((double)(8 / 12 + wall.Width), getVerticesFromPlanarFace(solidPlanarFace, 1/3),
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
								fireDumpersFromIntersection.Add(element);
								return true;
							}
						}
						if (!isFD)
						{
							return false;
						}
						break;
					}
				}
				return false;
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString() + " " + ductEl.Id.ToString());
				return false;
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