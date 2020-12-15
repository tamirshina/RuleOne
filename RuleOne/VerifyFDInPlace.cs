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
									//check for duplicates 
									if (ElementIsNotInTheList(ele))
									{
										whereIsFD.Add(ele);
										generalElList.Add(ductEl);
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
											generalElList.Add(ductEl);
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
			PrintResults("whereIsFD - Ducts", generalElList);
			PrintExceptions();

				ClearLists(); 		
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
		public bool IsMetalBeamIntersectsFW(Document doc, Element strucualBeam)
		{
			try
			{
				Solid beamSolid = TurnElToSolid(strucualBeam, GetTransform(doc, strucualBeam.Document.Title));
				//PaintSolid(doc, beamSolid, 1);
				PlanarFace face = GetBottomFaceOfSolid(doc, beamSolid);
				List<XYZ> vertices = GetVerticesFromPlanarFace(face, 1);
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
			Solid ductSolid = TurnElToSolid(ductEl, mepTransform);
			RevitLinkInstance linkedInstance = GetRevitLinkedInstance(doc, wallEl.Document.Title); 
			Wall wall = wallEl as Wall;
			List<Face> wallNormalFaces = FindWallNormalFace(wall);
			bool isFD = false;
			Solid intersectionSolid = null;
			try
			{
				foreach (Face wallFace in wallNormalFaces)
				{
					try
					{	
						Solid wallFaceSolid = TurnWallFaceToSolid(wallFace, linkedInstance);
						intersectionSolid = BooleanOperationsUtils.ExecuteBooleanOperation(wallFaceSolid, ductSolid, BooleanOperationsType.Intersect);
					}
					catch (Exception exc)
					{
						ExceptionFound.Add(exc.ToString() + " " + ductEl.Id.ToString());
						if (CheckIsFDWithScaledBB(doc, ductEl))
						{
							return true;
						}
						else { return false; }						
					}

					if (intersectionSolid.Volume > 0)
					{

						PlanarFace solidPlanarFace = GetIntersectionSolidRightFace(intersectionSolid, wallFace);
						if (solidPlanarFace != null)
						{
							Solid finalSolid = CreateSolidFromVertices((double)(8 / 12 + wall.Width), GetVerticesFromPlanarFace(solidPlanarFace, 1 / 3),
	solidPlanarFace.FaceNormal.Negate());
							Solid finalSolidInStrcModel = TransformSolid(linkedInstance.GetTransform(), Transform.Identity, finalSolid);

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
						else
						{
							if (CheckIsFDWithScaledBB(doc, ductEl))
							{
								return true;
							}
							else { return false; }
						}
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
        private bool CheckIsFDWithScaledBB(Document doc, Element el)
        {
			var buffer = new XYZ((double)1/2, (double)1 / 2, (double)1 / 2);
			try
			{
				foreach(Element ele in GetIntesectingDuctAccessory(doc, el, buffer))
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

        

	}
}