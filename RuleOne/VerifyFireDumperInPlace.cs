using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using static RuleOne.Helper;
using static RuleOne.Lists;

namespace RuleOne
{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class VerifyFireDumperInPlace : IExternalCommand
    {
		private const int NO_CHANGE_BUFFER = 1;
		private readonly double EIGHT_INCH_BUFFER = UnitUtils.ConvertToInternalUnits(8d, DisplayUnitType.DUT_DECIMAL_INCHES);
		private readonly double SIX_INCH_BUFFER = UnitUtils.ConvertToInternalUnits(6d, DisplayUnitType.DUT_DECIMAL_INCHES);
		private readonly double FOUR_INCH_BUFFER = UnitUtils.ConvertToInternalUnits(4d, DisplayUnitType.DUT_DECIMAL_INCHES);

		public Result Execute(ExternalCommandData revit, ref string message, ElementSet elements)
        {

			UIDocument uiDoc = revit.Application.ActiveUIDocument;
			Document activeDocument = uiDoc.Document;

			MainExecution(activeDocument);

			return Result.Succeeded;
        }
		public void MainExecution(Document activeDocument)
		{
            List <Model> allModels  = GetAllModels(activeDocument);
			List<Element> ducts = GetHorizontalDucts(allModels.Where(m => m.isMep));

			List<Element> whereIsFireDumper = CheckIsMissingFireDumper(activeDocument, ducts.Where(d => !AssertFireDumper(d)), allModels);

			PrintResults("whereIsFD", whereIsFireDumper);
			PrintExceptions();
			ClearLists(); 		
        }

        private List<Element> CheckIsMissingFireDumper(Document activeDocument, IEnumerable<Element> ducts, List<Model> allModels)
        {
			List<ResultType> missingFireDumper = new List<ResultType>();  
			try
			{
				foreach (Element duct in ducts)
				{
					foreach (Element ele in GetIntesectingElementsWithBoundingBox(activeDocument, duct, allModels))
					{
						if (IsFireRatedWall(ele))
						{
							if (!IsFireDumperInIntersection(activeDocument, duct, ele, allModels))
							{
								missingFireDumper.Add(new ResultType (ele, duct));								
							}
						}
						else if (IsMetalBeam(ele))
						{
							if (IsMetalBeamIntersectsFireRatedWall(activeDocument, ele, allModels))
							{
								if (!IsFireDumperInPlace(activeDocument, duct, allModels))
								{
									missingFireDumper.Add(new ResultType(ele, duct));
								}
							}
						}
					}
				}
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
			}
			
			return removeDuplicates(missingFireDumper);
		}
		private List<Element> removeDuplicates(List<ResultType> results)
		{
			HashSet<Element> noDuplicates = new HashSet<Element>();
			List<Element> wallsAndBeams = results.Select(w => w.FireWallOrMetalBeam).ToList();

			foreach (Element e in wallsAndBeams)
			{
				if (CheckDuplicates(e, noDuplicates))
				{
					noDuplicates.Add(e); 
				}
			}

			return noDuplicates.ToList();
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

        private bool IsFireDumperInPlace(Document activeDocument, Element duct, List<Model> allModels)
        {
			try
			{
				Model ductModel = allModels.Single(m => m.doc.Title == duct.Document.Title);
				Solid ductSolid = TurnElToSolid(duct, ductModel.transform);
                foreach (var _ in GetIntesectingElementsBySolid(ref ductSolid, allModels).Where(e => AssertFireDumper(e)))
                {
                    return true;
                }
            }
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
			}
			return false;
        }
		public bool IsMetalBeamIntersectsFireRatedWall(Document activeDocument, Element structuralBeam, List<Model> allModels)
		{
			try
			{
				Model structuralBeamModel = allModels.Single(m => m.doc.Title == structuralBeam.Document.Title);
				Solid beamSolid = TurnElToSolid(structuralBeam, structuralBeamModel.transform);
				PlanarFace face = GetBottomFace(beamSolid);
				List<XYZ> vertices = GetVerticesFromPlanarFace(face, NO_CHANGE_BUFFER);
				Solid faceSolid = CreateSolidFromVertices(FOUR_INCH_BUFFER, vertices, -XYZ.BasisZ);
                foreach (var _ in GetIntesectingElementsBySolid(ref faceSolid, allModels).Where(ele => IsFireRatedWall(ele)))
                {
                    return true;
                }

                return false;
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
				return false;
			}
		}
		public PlanarFace GetBottomFace(Solid solid)
		{
			PlanarFace planarFace =null;
			foreach (Face face in solid.Faces)
			{
				if (face is PlanarFace planar)
				{
					if (planar.FaceNormal.IsAlmostEqualTo(-XYZ.BasisZ))
					{
						planarFace = planar;
					}
				}
			}
			return planarFace;

		}
		public bool IsFireDumperInIntersection(Document activeDocument, Element duct, Element wallAsElement, List<Model> allModels)
		{
			Model ductModel = allModels.Single(m => m.doc.Title == duct.Document.Title);
			Transform mepTransform = ductModel.transform;
			Solid ductSolid = TurnElToSolid(duct, mepTransform);
			RevitLinkInstance linkedInstance = GetRevitLinkedInstance(activeDocument, wallAsElement.Document.Title); 
			Wall wall = wallAsElement as Wall;
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
						ExceptionFound.Add(exc.ToString() + " " + duct.Id.ToString());
						if (CheckIsFDWithScaledBB(activeDocument, duct, allModels))
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
							Solid finalSolid = CreateSolidFromVertices((double)(EIGHT_INCH_BUFFER + wall.Width),
								GetVerticesFromPlanarFace(solidPlanarFace, (int)FOUR_INCH_BUFFER), solidPlanarFace.FaceNormal.Negate());

							Solid finalSolidInLinkedModel = TransformSolid(linkedInstance.GetTransform(), Transform.Identity, finalSolid);
							
							FilteredElementCollector collector = new FilteredElementCollector(GetDocument(activeDocument, "MEP"));

							collector.WherePasses(new ElementIntersectsSolidFilter(finalSolidInLinkedModel));

							foreach (Element element in collector)
							{
								if (AssertFireDumper(element))
								{
									isFD = true;
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
							if (CheckIsFDWithScaledBB(activeDocument, duct, allModels))
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
				ExceptionFound.Add(exc.ToString() + " " + duct.Id.ToString());
				return false;
			}
		}
        private bool CheckIsFDWithScaledBB(Document activeDocument, Element el, List<Model> allModels)
        {
			var buffer = new XYZ(SIX_INCH_BUFFER, SIX_INCH_BUFFER, SIX_INCH_BUFFER);
			try
			{
				foreach(Element ele in GetIntesectingDuctAccessory(activeDocument, el, buffer, allModels))
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