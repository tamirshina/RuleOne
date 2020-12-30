using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;


namespace RuleOne
{
    public static class IntersectionHelper
    {
		public static HashSet<Element> GetIntesectingDuctAccessory(Document activeDocument, Element elementToIntersect, XYZ buffer, List<Model> allModels)
		{
			HashSet<Element> elementList = new HashSet<Element>();
			Model elementToIntersectModel = allModels.Single(m => m.doc.Title == elementToIntersect.Document.Title);

			foreach (Model linkedInstance in allModels)
			{
				try
				{
					var bb = elementToIntersect.get_BoundingBox(null);

					if (bb != null)
					{
						var filter = new BoundingBoxIntersectsFilter(new Outline(ExecutionHelper.TransformPoint(bb.Min.Subtract(buffer), elementToIntersectModel.transform,
							linkedInstance.transform), ExecutionHelper.TransformPoint(bb.Max.Add(buffer), elementToIntersectModel.transform,
							linkedInstance.transform)));

						FilteredElementCollector collector = new FilteredElementCollector(linkedInstance.doc);

						List<Element> intersectsList = collector.WherePasses(filter).WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_DuctAccessory)).ToList();

						foreach (Element intersectionEl in intersectsList)
						{
							elementList.Add(intersectionEl);
						}
					}
				}
				catch (Exception exc)
				{
					Constants.ExceptionFound.Add(exc.ToString());
				}
			}
			return elementList;
		}
		public static HashSet<Element> GetIntesectingElementsBySolid(ref Solid solidToIntersect, List<Model> allModels)
		{
			HashSet<Element> elementList = new HashSet<Element>();

			foreach (Model model in allModels)
			{
				try
				{
					if (!model.transform.AlmostEqual(Transform.Identity))
					{
						solidToIntersect = SolidUtils.CreateTransformed(
						  solidToIntersect, model.transform.Inverse);
					}

					List<Element> intersectsList = new FilteredElementCollector(model.doc)
						.WherePasses(new ElementIntersectsSolidFilter(solidToIntersect))
						.Cast<Element>()
						.ToList();

					foreach (Element intersectionEl in intersectsList)
					{
						elementList.Add(intersectionEl);
					}

				}
				catch (Exception exc)
				{
					Constants.ExceptionFound.Add(exc.ToString());
				}
			}
			return elementList;
		}
		public static HashSet<Element> GetIntesectingElementsWithBoundingBox(Document activeDocument, Element elementToIntersect,
	List<Model> allModels)
		{
			HashSet<Element> elementList = new HashSet<Element>();

			foreach (Model model in allModels)
			{
				try
				{
					var bb = elementToIntersect.get_BoundingBox(null);

					if (bb != null)
					{

						Model elementToIntersectModel = allModels.Single(m => m.doc.Title == elementToIntersect.Document.Title);

						var filter = new BoundingBoxIntersectsFilter(new Outline(ExecutionHelper.TransformPoint(bb.Min, elementToIntersectModel.transform, model.transform),
							ExecutionHelper.TransformPoint(bb.Max, elementToIntersectModel.transform, model.transform)));

						FilteredElementCollector collector = new FilteredElementCollector(model.doc);

						List<Element> intersectsList = collector.WherePasses(filter).WherePasses(new ElementMulticategoryFilter(Constants.intersectionElementsCatagories)).ToList();

						foreach (Element intersectionEl in intersectsList)
						{
							elementList.Add(intersectionEl);
						}
					}
				}
				catch (Exception exc)
				{
					Constants.ExceptionFound.Add(exc.ToString());
				}
			}
			return elementList;
		}
		public static bool IsMetalBeamIntersectsFireRatedWall(Document activeDocument, Element structuralBeam, List<Model> allModels)
		{
			try
			{
				Model structuralBeamModel = allModels.Single(m => m.doc.Title == structuralBeam.Document.Title);
				Solid beamSolid = SolidHelper.TurnElementToItsLargestSolid(structuralBeam, structuralBeamModel.transform);
				PlanarFace face = ExecutionHelper.GetBottomFace(beamSolid);
				List<XYZ> vertices = ExecutionHelper.getPlanarFaceVertices(face, Constants.NO_CHANGE_BUFFER);
				Solid faceSolid = SolidHelper.CreateSolidFromVertices(Constants.FOUR_INCH_BUFFER, vertices, -XYZ.BasisZ);
				foreach (var _ in GetIntesectingElementsBySolid(ref faceSolid, allModels).Where(ele => IdentificationHelper.IsFireRatedWall(ele)))
				{
					return true;
				}

				return false;
			}
			catch (Exception exc)
			{
				Constants.ExceptionFound.Add(exc.ToString());
				return false;
			}
		}
		private static bool IsFireDumperInPlace(Document activeDocument, Element duct, List<Model> allModels)
		{
			try
			{
				Model ductModel = allModels.Single(m => m.doc.Title == duct.Document.Title);
				Solid ductSolid = SolidHelper.TurnElementToItsLargestSolid(duct, ductModel.transform);
				foreach (var _ in GetIntesectingElementsBySolid(ref ductSolid, allModels).Where(e => IdentificationHelper.IsFireDumper(e)))
				{
					return true;
				}
			}
			catch (Exception exc)
			{
				Constants.ExceptionFound.Add(exc.ToString());
			}
			return false;
		}
		public static List<Element> CheckIsMissingFireDumper(Document activeDocument, IEnumerable<Element> ducts, List<Model> allModels)
		{
			List<ResultType> missingFireDumper = new List<ResultType>();
			try
			{
				foreach (Element duct in ducts)
				{
					foreach (Element element in GetIntesectingElementsWithBoundingBox(activeDocument, duct, allModels))
					{
						if (IdentificationHelper.IsFireRatedWall(element))
						{
							if (!IsFireDumperInIntersection(activeDocument, duct, element, allModels))
							{
								missingFireDumper.Add(new ResultType(element, duct));
							}
						}
						else if (IdentificationHelper.IsMetalBeam(element))
						{
							if (IsMetalBeamIntersectsFireRatedWall(activeDocument, element, allModels))
							{
								if (!IsFireDumperInPlace(activeDocument, duct, allModels))
								{
									missingFireDumper.Add(new ResultType(element, duct));
								}
							}
						}
					}
				}
			}
			catch (Exception exc)
			{
				Constants.ExceptionFound.Add(exc.ToString());
			}

			return ExecutionHelper.removeDuplicates(missingFireDumper);
		}
		public static bool IsFireDumperInIntersection(Document activeDocument, Element duct, Element wallAsElement, List<Model> allModels)
		{
			Model ductModel = allModels.Single(m => m.doc.Title == duct.Document.Title);
			Transform mepTransform = ductModel.transform;
			Solid ductSolid = SolidHelper.TurnElementToItsLargestSolid(duct, mepTransform);
			RevitLinkInstance linkedInstance = ExecutionHelper.GetRevitLinkedInstance(activeDocument, wallAsElement.Document.Title);
			Wall wall = wallAsElement as Wall;
			List<Face> wallNormalFaces = ExecutionHelper.getWallSideFaces(wall);
			bool isFD = false;
			Solid intersectionSolid = null;
			try
			{
				foreach (Face wallFace in wallNormalFaces)
				{
					try
					{
						Solid wallFaceSolid = SolidHelper.TurnWallFaceToSolid(wallFace, linkedInstance);
						intersectionSolid = BooleanOperationsUtils.ExecuteBooleanOperation(wallFaceSolid, ductSolid, BooleanOperationsType.Intersect);
					}
					catch (Exception exc)
					{
						Constants.ExceptionFound.Add(exc.ToString() + " " + duct.Id.ToString());
						if (doesElementBoundingBoxIntersectWithFireDumper(activeDocument, duct, allModels))
						{
							return true;
						}
						else { return false; }
					}

					if (intersectionSolid.Volume > 0)
					{
						PlanarFace planarFace = IdentificationHelper.GetWallAndIntersectionSolidJoinedPlananrFace(intersectionSolid, wallFace);
						if (planarFace != null)
						{
							Solid increasedSolid = SolidHelper.CreateSolidFromVertices((double)(Constants.EIGHT_INCH_BUFFER + wall.Width),
								ExecutionHelper.getPlanarFaceVertices(planarFace, (int)Constants.FOUR_INCH_BUFFER), planarFace.FaceNormal.Negate());

							foreach (Model model in allModels.Where(m => m.isMep))
							{
								Solid increasedSolidInLinkedModel = ExecutionHelper.TransformSolid(linkedInstance.GetTransform(), Transform.Identity, increasedSolid);
								FilteredElementCollector collector = new FilteredElementCollector(model.doc);

								collector.WherePasses(new ElementIntersectsSolidFilter(increasedSolidInLinkedModel));

								foreach (Element element in collector)
								{
									if (IdentificationHelper.IsFireDumper(element))
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

						}
						else
						{
							if (doesElementBoundingBoxIntersectWithFireDumper(activeDocument, duct, allModels))
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
				Constants.ExceptionFound.Add(exc.ToString() + " " + duct.Id.ToString());
				return false;
			}
		}
		private static bool doesElementBoundingBoxIntersectWithFireDumper(Document activeDocument, Element intersectedElement, List<Model> allModels)
		{
			var buffer = new XYZ(Constants.SIX_INCH_BUFFER, Constants.SIX_INCH_BUFFER, Constants.SIX_INCH_BUFFER);
			try
			{
				foreach (Element element in GetIntesectingDuctAccessory(activeDocument, intersectedElement, buffer, allModels))
				{
					if (IdentificationHelper.IsFireDumper(element))
					{
						return true;
					}
				}
			}
			catch (Exception exc)
			{
				Constants.ExceptionFound.Add(exc.ToString());
			}
			return false;
		}
	}
}
