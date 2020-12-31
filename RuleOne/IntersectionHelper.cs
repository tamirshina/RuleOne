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
				Solid faceSolid = SolidHelper.CreateSolidFromVertices(Constants.FOUR_INCH_BUFFER, vertices, -XYZ.BasisZ, activeDocument);

				return GetIntesectingElementsBySolid(ref faceSolid, allModels).Any(ele => IdentificationHelper.IsFireRatedWall(ele));
			}
			catch (Exception exc)
			{
				Constants.ExceptionFound.Add(exc.ToString());
				return false;
			}
		}
		
		public static List<Element> GetIntersectingElementsBySolidOnSingleModel(Model model, Solid solid)
		{
			FilteredElementCollector collector = new FilteredElementCollector(model.doc);

			return collector.WherePasses(new ElementIntersectsSolidFilter(solid)).ToList();
		}
        public static Solid GetIntersectionSolid(Element duct, Face wallFace, RevitLinkInstance linkedInstance, Solid ductSolid)
		{
			try
			{
				Solid wallFaceSolid = SolidHelper.TurnWallFaceToSolid(wallFace, linkedInstance);
				return BooleanOperationsUtils.ExecuteBooleanOperation(wallFaceSolid, ductSolid, BooleanOperationsType.Intersect);
			}
			catch (Exception exc)
			{
				Constants.ExceptionFound.Add(exc.ToString() + " " + duct.Id.ToString());
				return null;
			}
		}
	}
}
