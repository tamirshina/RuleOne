using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;

namespace RuleOne
{
	[Transaction(TransactionMode.Manual)]
	[Regeneration(RegenerationOption.Manual)]
	public static class ExecutionHelper
	{
        public static List<Element> GetHorizontalDucts(IEnumerable<Model> mepModels)
		{
			List<Element> horizontalDucts = null;
			try
			{
				ElementMulticategoryFilter ductCategoriesFilter = new ElementMulticategoryFilter(Constants.ductCategories);

				foreach (var model in mepModels)
				{
					horizontalDucts = new FilteredElementCollector(model.doc)
						.WherePasses(ductCategoriesFilter)
						.Cast<Element>()
						.Where(e => IdentificationHelper.IsHorizontal(e)).ToList();
				}
				
			}
			catch (Exception exception)
			{
				Constants.ExceptionFound.Add(exception.ToString());
			}
			return horizontalDucts;
		}
		
		public static Solid TransformSolid(Transform targetTransform, Transform sourceTransform, Solid solid)
		{
			var transform = targetTransform.Multiply(sourceTransform);
			var solidInTargetModel = SolidUtils.CreateTransformed(solid, transform);
			return solidInTargetModel;
		}
		public static bool CheckDuplicates(Element ele, HashSet<Element> whereIsFD)
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
		public static PlanarFace GetBottomFace(Solid solid)
		{
			PlanarFace planarFace = null;
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
		public static RevitLinkInstance GetRevitLinkedInstance(Document activeDocument, string target)
		{
			var models = new FilteredElementCollector(activeDocument).OfClass(typeof(RevitLinkInstance));
			foreach (var m in models)
			{
				var linkedModel = ((RevitLinkInstance)m);
				var tempDoc = linkedModel.GetLinkDocument();
				if (tempDoc.Title.Contains(target))
				{
					return linkedModel;
				}
			}
			return null;
		}
		public static XYZ TransformPoint(XYZ point, Transform sourceTransform, Transform targetTransform)
		{
			var pointInHost = sourceTransform.OfPoint(point);
			var pointInTargetTransform = targetTransform.OfPoint(pointInHost);

			return pointInTargetTransform;
		}
		public static List<XYZ> getPlanarFaceVertices(PlanarFace planarFace, int buffer)
		{
			var bufferInFeet = planarFace.FaceNormal.Multiply(buffer);
			var vertices = planarFace.Triangulate()
				.Vertices
				.Select(x => x.Add(bufferInFeet))
				.ToList();
			return vertices;
		}
		public static List<Face> getWallSideFaces(Wall wall)
		{
			List<Face> normalFaces = new List<Face>();

			Options opt = new Options();
			opt.ComputeReferences = true;
			opt.DetailLevel = ViewDetailLevel.Fine;
			var geometryElement = wall.get_Geometry(new Options());
            var solids = geometryElement.Where(o => o is Solid).Select(s => s as Solid);

			foreach (Solid solid in solids)
			{
				if (solid.Faces.Size > 0)
				{
					foreach (Face face in solid.Faces)
					{
						PlanarFace pf = face as PlanarFace;
						if (!(pf.FaceNormal == new XYZ(0, 0, 1)) && !(pf.FaceNormal == new XYZ(0, 0, -1)))
						{
							normalFaces.Add(pf);
						}
					}
				}
			}
			return normalFaces;
		}
		public static ICollection<ElementId> GetIdsFromEls(HashSet<Element> elList)
		{
			ICollection<ElementId> listOfIds = new List<ElementId>();
			foreach (Element el in elList)
			{
				listOfIds.Add(el.Id);
			}
			return listOfIds;
		}
		public static List<Element> removeDuplicates(List<ResultType> results)
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
	}
}
