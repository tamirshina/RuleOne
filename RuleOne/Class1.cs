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
    public class Class1 : IExternalCommand
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
		private List<Element> GetIntesectingWalls(Element elToIntersect, Document targetDoc,Transform targetTransform, Transform sourceTransform, Document doc)
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
		private PlanarFace getIntersectionSolidRightFace(Solid intersectionSolid, Face wallFace)
		{
			foreach (Face fa in intersectionSolid.Faces)
			{
				PlanarFace solidPlanarFace = fa as PlanarFace;
				PlanarFace wallPlanarFace = wallFace as PlanarFace;
				if (solidPlanarFace.FaceNormal.IsAlmostEqualTo(wallPlanarFace.FaceNormal))
				{
					return solidPlanarFace;
				}
			}
			return null;
		}
		public void findIntersectionPoint(Document doc, Element ductEl, Element wallEl, Transform mepModel, Transform wallDoc)
		{
			Solid ductSolid = TurnElToSolid(ductEl, mepModel);

			Wall wall = wallEl as Wall;
			List<Face> wallFaceSolids = FindWallFace(wall);
            bool isFD = false;

			try
			{
				foreach (Face wallFace in wallFaceSolids)
				{
					PlanarFace wallPlanarFace = wallFace as PlanarFace;
					Solid wallFaceSolid = TurnWallFaceToSolid(wallPlanarFace);

					var intersectionSolid = BooleanOperationsUtils.ExecuteBooleanOperation(wallFaceSolid, ductSolid, BooleanOperationsType.Intersect);//check why is the exception.

					if (intersectionSolid.Volume > 0)
					{
						PlanarFace solidPlanarFace = getIntersectionSolidRightFace(intersectionSolid, wallFace);


                        Solid finalSolid = CreateSolidFromVertices((double)(8 / 12 + wall.Width), getVerticesFromPlanarFace(solidPlanarFace), solidPlanarFace.FaceNormal.Negate());
                        PaintSolid(doc, finalSolid, 1);
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
				Element ele = wallEl;
			}
		}
		private List<XYZ> getVerticesFromPlanarFace(PlanarFace planarFace)
		{
			var bufferInDirection = planarFace.FaceNormal.Multiply(1 / 3);
			var vertices = planarFace.Triangulate()
				.Vertices
				.Select(x => x.Add(bufferInDirection))
				.ToList();
			return vertices;
		}
		private Solid TurnWallFaceToSolid(PlanarFace face)
		{
			var vertices = face.Triangulate()
				.Vertices
				.ToList();
			try
			{
				Solid someSolid = CreateSolidFromVerticesWithCurveLoop((double)1 / 12, vertices, face.FaceNormal, face.GetEdgesAsCurveLoops());
				return someSolid;
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString() + "-TurnWallFaceToSolid-");
				return null;
			}
			
		}
		private Solid TurnElToSolid(Element el, Transform linkedDoc)
		{
			Solid solid = null;
			Transform trans = null;
			int largestVol = 0; 

			try
			{
				var geo1 = el.get_Geometry(new Options());
				var solids = geo1.Where(o => o is Solid);
				foreach (var g in solids)
				{
					Solid s = g as Solid;

					if (s.Volume > largestVol)
					{
						solid = s;
					}
				}
				trans = linkedDoc.Inverse;
			}
			catch(Exception exc)
			{
				ExceptionFound.Add(exc.ToString()
					);
			}
			return SolidUtils.CreateTransformed(solid, trans);
		}
		public Solid CreateSolidFromVertices(double height, List<XYZ> vertices, XYZ direction)
		{
			try
			{

				var edges = new List<Curve>();
				for (int i = 0; i < vertices.Count - 1; i++)
				{
					edges.Add(Line.CreateBound(vertices[i], vertices[i + 1]));
				}
				edges.Add(Line.CreateBound(vertices.Last(), vertices.First()));
				CurveLoop baseLoop = CurveLoop.Create(edges);
				List<CurveLoop> loopList = new List<CurveLoop>
				{
					baseLoop
				};
				Solid preTransformBox = GeometryCreationUtilities.CreateExtrusionGeometry(loopList, direction,
																						  height);
				Solid transformBox = SolidUtils.CreateTransformed(preTransformBox, Transform.Identity);
				return transformBox;
			}
		
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString() + Environment.NewLine);
				return null;
			}
}
		public Solid CreateSolidFromVerticesWithCurveLoop(double height, List<XYZ> vertices, XYZ direction, IList<CurveLoop> loopList)
		{
			try
			{
				var edges = new List<Curve>();
				for (int i = 0; i < vertices.Count - 1; i++)
				{
					edges.Add(Line.CreateBound(vertices[i], vertices[i + 1]));
				}
				edges.Add(Line.CreateBound(vertices.Last(), vertices.First()));
				Solid preTransformBox = GeometryCreationUtilities.CreateExtrusionGeometry(loopList, direction,
																						  height);
				Solid transformBox = SolidUtils.CreateTransformed(preTransformBox, Transform.Identity);
				return transformBox;
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString() + Environment.NewLine);
				return null;
			}
				
		}
		public List<Face> FindWallFace(Wall wall)
		{
			List<Face> normalFaces = new List<Face>();

			Options opt = new Options();
			opt.ComputeReferences = true;
			opt.DetailLevel = ViewDetailLevel.Fine;

			GeometryElement e = wall.get_Geometry(opt);

			foreach (GeometryObject obj in e)
			{
				Solid solid = obj as Solid;

				if (solid != null && solid.Faces.Size > 0)
				{
					foreach (Face face in solid.Faces)
					{
						PlanarFace pf = face as PlanarFace;
						if (!(pf.FaceNormal == new XYZ(0, 0, 1)) || !(pf.FaceNormal == new XYZ(0, 0, -1)))
						{
							normalFaces.Add(pf);							
						}
					}
				}
			}
			return normalFaces;
		}

	}
}