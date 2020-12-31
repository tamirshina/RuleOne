using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;


namespace RuleOne
{
    public static class FireDumperFinder
    {
		private static bool IsFireDumperInPlace(Element duct, List<Model> allModels)
		{
			try
			{
				Model ductModel = allModels.Single(m => m.doc.Title == duct.Document.Title);
				Solid ductSolid = SolidHelper.TurnElementToItsLargestSolid(duct, ductModel.transform);
				return IntersectionHelper.GetIntesectingElementsBySolid(ref ductSolid, allModels).Any(e => IdentificationHelper.IsFireDumper(e));
			}
			catch (Exception exc)
			{
				Constants.ExceptionFound.Add(exc.ToString());
				return false;
			}

		}
		public static bool IsMissingFireDumper(Element duct, Element element, Document activeDocument, List<Model> allModels)
		{
			return (IdentificationHelper.IsFireRatedWall(element) && !IsFireDumperInIntersection(activeDocument, duct, element, allModels)) ||
				(IdentificationHelper.IsMetalBeam(element) && (IntersectionHelper.IsMetalBeamIntersectsFireRatedWall(activeDocument, element, allModels) && !IsFireDumperInPlace(duct, allModels)));
		}
		public static List<Element> CheckIsMissingFireDumper(Document activeDocument, IEnumerable<Element> ducts, List<Model> allModels)
		{
			List<ResultType> missingFireDumpers = new List<ResultType>();
			try
			{
				missingFireDumpers = ducts.SelectMany(d =>
				IntersectionHelper.GetIntesectingElementsWithBoundingBox(activeDocument, d, allModels)
						   .Where(i => IsMissingFireDumper(d, i, activeDocument, allModels))
						   .Select(x => new ResultType(x, d))
			   ).ToList();
			}
			catch (Exception exc)
			{
				Constants.ExceptionFound.Add(exc.ToString());
			}

			return ExecutionHelper.removeDuplicates(missingFireDumpers);
		}
		public static bool IsFireDumperInIntersection(Document activeDocument, Element duct, Element wallAsElement, List<Model> allModels)
		{
			Model ductModel = allModels.Single(m => m.doc.Title == duct.Document.Title);
			Solid ductSolid = SolidHelper.TurnElementToItsLargestSolid(duct, ductModel.transform);
			RevitLinkInstance linkedInstance = ExecutionHelper.GetRevitLinkedInstance(activeDocument, wallAsElement.Document.Title);
			Wall wall = wallAsElement as Wall;
			List<Face> wallSideFaces = ExecutionHelper.getWallSideFaces(wall);
			try
			{
				return wallSideFaces.Any(wallFace => IsFireDumperInPalce(activeDocument, allModels, duct, wallFace, linkedInstance, ductSolid, wall));

			}
			catch (Exception exc)
			{
				Constants.ExceptionFound.Add(exc.ToString() + " " + duct.Id.ToString());
				return false;
			}
		}

		private static bool IsFireDumperInPalce(Document activeDocument, List<Model> allModels, Element duct, Face wallFace, RevitLinkInstance linkedInstance, Solid ductSolid, Wall wall)
		{
			Solid intersectionSolid = IntersectionHelper.GetIntersectionSolid(duct, wallFace, linkedInstance, ductSolid);

			if (intersectionSolid == null)
			{
				return DoesElementBoundingBoxIntersectWithFireDumper(activeDocument, duct, allModels);
			}
			else
			{
				return intersectionSolid.Volume > 0 && IsFireDumperInWallDuctIntersection(activeDocument, allModels, duct, wallFace, linkedInstance, ductSolid, intersectionSolid, wall);
			}
		}

		private static bool IsFireDumperInWallDuctIntersection(Document activeDocument, List<Model> allModels, Element duct, Face wallFace, RevitLinkInstance linkedInstance, Solid ductSolid, Solid intersectionSolid, Wall wall)
		{
			PlanarFace planarFace = IdentificationHelper.GetWallAndIntersectionSolidJoinedPlananrFace(intersectionSolid, wallFace);
			if (planarFace != null)
			{
				Solid increasedSolid = SolidHelper.CreateSolidFromVertices((double)(Constants.EIGHT_INCH_BUFFER + wall.Width),
					ExecutionHelper.getPlanarFaceVertices(planarFace, (int)Constants.FOUR_INCH_BUFFER), planarFace.FaceNormal.Negate());

				return allModels.Where(m => m.isMep).Any(m => IsFireDumperIntersectsIncreasedSolid(m, linkedInstance, increasedSolid));
			}
			else
			{
				return DoesElementBoundingBoxIntersectWithFireDumper(activeDocument, duct, allModels);

			}
		}
		private static bool IsFireDumperIntersectsIncreasedSolid(Model model, RevitLinkInstance linkedInstance, Solid increasedSolid)
		{

			Solid increasedSolidInLinkedModel = ExecutionHelper.TransformSolid(linkedInstance.GetTransform(), Transform.Identity, increasedSolid);

			return IntersectionHelper.GetIntersectingElementsBySolidOnSingleModel(model, increasedSolidInLinkedModel).Where(e => IdentificationHelper.IsFireDumper(e)).Any();

		}
		private static bool DoesElementBoundingBoxIntersectWithFireDumper(Document activeDocument, Element intersectedElement, List<Model> allModels)
		{
			var buffer = new XYZ(Constants.SIX_INCH_BUFFER, Constants.SIX_INCH_BUFFER, Constants.SIX_INCH_BUFFER);
			try
			{
				foreach (Element element in IntersectionHelper.GetIntesectingDuctAccessory(activeDocument, intersectedElement, buffer, allModels))
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
