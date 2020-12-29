using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.UI;
using static RuleOne.Lists;
namespace RuleOne
{
	[Transaction(TransactionMode.Manual)]
	[Regeneration(RegenerationOption.Manual)]
	public static class Helper
	{
        private static double ONE_INCH_BUFFER = UnitUtils.ConvertToInternalUnits(1d, DisplayUnitType.DUT_DECIMAL_INCHES);

        public static List<Element> GetHorizontalDucts(IEnumerable<Model> mepModels)
		{
			List<Element> horizontalDucts = null;
			try
			{
				ElementMulticategoryFilter ductCategoriesFilter = new ElementMulticategoryFilter(ductCategories);

				foreach (var model in mepModels)
				{
					horizontalDucts = new FilteredElementCollector(model.doc)
						.WherePasses(ductCategoriesFilter)
						.Cast<Element>()
						.Where(e => IsHrizontal(e)).ToList();
				}
				
			}
			catch (Exception exception)
			{
				ExceptionFound.Add(exception.ToString());
			}
			return horizontalDucts;
		}
		public static bool IsMetalBeam(Element el)
		{
			BuiltInCategory structuralBeam = BuiltInCategory.OST_StructuralFraming;
			BuiltInCategory elementCategory = (BuiltInCategory)el.Category.Id.IntegerValue;

			return structuralBeam.Equals(elementCategory);
	
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
		public static void ClearLists()
		{
			ExceptionFound.Clear();

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

						var filter = new BoundingBoxIntersectsFilter(new Outline(TransformPoint(bb.Min, elementToIntersectModel.transform, model.transform), 
							TransformPoint(bb.Max, elementToIntersectModel.transform, model.transform)));

						FilteredElementCollector collector = new FilteredElementCollector(model.doc);

						List<Element> intersectsList = collector.WherePasses(filter).WherePasses(new ElementMulticategoryFilter(intersectionElementsCatagories)).ToList();

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
						var filter = new BoundingBoxIntersectsFilter(new Outline(TransformPoint(bb.Min.Subtract(buffer), elementToIntersectModel.transform,
							linkedInstance.transform), TransformPoint(bb.Max.Add(buffer), elementToIntersectModel.transform,
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
					ExceptionFound.Add(exc.ToString());
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
					ExceptionFound.Add(exc.ToString());
				}
			}
			return elementList;
		}
		public static Document GetDocument(Document activeDocument, string target)
		{
			var models = new FilteredElementCollector(activeDocument).OfClass(typeof(RevitLinkInstance));
			foreach (var m in models)
			{
				var linkedModel = ((RevitLinkInstance)m); //m as RevitLinkInstance;
				var tempDoc = linkedModel.GetLinkDocument();
				if (tempDoc.Title.Contains(target))
				{
					return tempDoc;
				}
			}
			return null;
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
		public static void PrintResults(string headline, List<Element> elList)
		{
			string info = "";
			string name = "";

			foreach (Element duct in elList)
			{
				if (duct != null)
				{
					name = duct.Name;
					info += name + " " + duct.Id + "  " + "catagory"  + " " + (BuiltInCategory)duct.Category.Id.IntegerValue +  Environment.NewLine;
				}

			}
			TaskDialog.Show("revit", "Count: " + elList.Count() + Environment.NewLine + headline + "-"
							+ Environment.NewLine + info + Environment.NewLine);
		}
		public static bool IsFireRatedWall(Element element)
		{
			try
			{
				if (element is Wall wall)
				{
					//check Interior
					if (wall.WallType.Function.ToString() == "Interior" || wall.WallType.Function.ToString() != "Exterior")
					{
						//check DOOR_FIRE_RATING positive value
						if (!String.IsNullOrEmpty(wall.WallType.get_Parameter(BuiltInParameter.DOOR_FIRE_RATING).AsString()) &&
							  wall.WallType.get_Parameter(BuiltInParameter.DOOR_FIRE_RATING).AsString() != "DO NOT USE")
						{
							return true;
						}
						else
						{
							if (wall.WallType.get_Parameter(BuiltInParameter.DOOR_FIRE_RATING).AsString() == "DO NOT USE")
							{
								return false;
							}
							foreach (string str in fireRatedNameOptions)
							{
								if (wall.Name.ToLower().Contains(str.ToLower()))
								{
									return true;
								}
							}
							return false;
						}
					}
					else { return false; }
				}
				else { return false; }
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
				return false;
			}
		}
		public static bool AssertFireDumper(Element e)
		{
			try
			{
				string ductName = "";
				foreach (string str in optionalFamiliesNames)
				{
					if (e is FamilyInstance fInstance)
					{
						FamilySymbol FType = fInstance.Symbol;
						Family Fam = FType.Family;
						ductName = Fam.Name;
					}
					else
					{
						if (e is Duct duct)
						{
							ductName = duct.DuctType.Name;
						}
					}
					foreach (string excludeStr in optionalFamiliesNamesToExclude)
					{
						if (ductName.ToLower().Contains(excludeStr))
						{
							return false;
						}
						else
						{
							if (ductName.ToLower().Contains(str))
							{
								return true;
							}
						}
					}
				}
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
				return false;
			}
			return false;
		}
		public static bool IsHrizontal(Element el)
		{
			//duct.ConnectorManager.Connectors.IsEmpty
			try
			{
				Duct duct = el as Duct;

				if (duct != null)
				{
					Parameter param = duct.get_Parameter(BuiltInParameter.RBS_START_OFFSET_PARAM);
					if (param != null)
					{
						Parameter param2 = duct.get_Parameter(BuiltInParameter.RBS_END_OFFSET_PARAM);
						if (param != null & param2 != null)
						{
							double testParam = Math.Truncate(param.AsDouble() * 100000) / 100000;
							double testParam2 = Math.Truncate(param2.AsDouble() * 100000) / 100000;

							if (testParam == testParam2)
							{
								return true;
							}
							else { return false; }
						}
					}
				}
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
				return true;
			}
			return true;
		}
		public static PlanarFace GetIntersectionSolidRightFace(Solid intersectionSolid, Face wallFace)
		{
			PlanarFace wallPlanarFace = wallFace as PlanarFace;

			foreach (Face fa in intersectionSolid.Faces)
			{
				try
				{
					PlanarFace solidPlanarFace = fa as PlanarFace;

					if (solidPlanarFace != null && solidPlanarFace.FaceNormal.IsAlmostEqualTo(wallPlanarFace.FaceNormal))
					{
						return solidPlanarFace;
					}
				}
				catch (Exception exc)
				{
					ExceptionFound.Add(exc.ToString());
					continue;
				}
			}
			return null;
		}
		public static List<XYZ> GetVerticesFromPlanarFace(PlanarFace planarFace, int buffer)
		{
			var bufferInDirection = planarFace.FaceNormal.Multiply(buffer);
			var vertices = planarFace.Triangulate()
				.Vertices
				.Select(x => x.Add(bufferInDirection))
				.ToList();
			return vertices;
		}
		public static Solid TurnWallFaceToSolid(Face face, RevitLinkInstance linkedInstance)
		{
			PlanarFace wallPlanarFace = face as PlanarFace;
			var vertices = wallPlanarFace.Triangulate()
				.Vertices
				.ToList();
			try
			{
				Solid faceSolid = CreateSolidFromVerticesWithCurveLoop(ONE_INCH_BUFFER, vertices, wallPlanarFace.FaceNormal,
					wallPlanarFace.GetEdgesAsCurveLoops(), linkedInstance);
				return faceSolid;
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString() + "-TurnWallFaceToSolid-");
				return null;
			}

		}
		public static Solid TurnElToSolid(Element el, Transform linkedTransform)
		{
			Solid solid = null;
			double largestVol = 0;

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
						largestVol = s.Volume;
					}
				}
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString());
			}
			return SolidUtils.CreateTransformed(solid, linkedTransform);
		}
		public static Solid CreateSolidFromVertices(double height, List<XYZ> vertices, XYZ direction)
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
		public static Solid CreateSolidFromVerticesWithCurveLoop(double height, List<XYZ> vertices, XYZ direction,
			IList<CurveLoop> loopList, RevitLinkInstance linkedInstance)
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
				Solid transformBox = SolidUtils.CreateTransformed(preTransformBox, linkedInstance.GetTransform());
				return transformBox;
			}
			catch (Exception exc)
			{
				ExceptionFound.Add(exc.ToString() + Environment.NewLine);
				return null;
			}
		}
		public static List<Face> FindWallNormalFace(Wall wall)
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
						if (!(pf.FaceNormal == new XYZ(0, 0, 1)) && !(pf.FaceNormal == new XYZ(0, 0, -1)))
						{
							normalFaces.Add(pf);
						}
					}
				}
			}
			return normalFaces;
		}
		public static void PaintSolid(Document activeDocument, Solid s, double value)
		{
			int schemaId = -1;
			var rnd = new Random();

			View view = activeDocument.ActiveView;

			using (Transaction transaction = new Transaction(activeDocument))
			{
				if (transaction.Start("Create model curves") == TransactionStatus.Started)
				{
					if (view.AnalysisDisplayStyleId == ElementId.InvalidElementId)
						CreateAVFDisplayStyle(activeDocument, view);

					SpatialFieldManager sfm = SpatialFieldManager.GetSpatialFieldManager(view);
					if (null == sfm)
						sfm = SpatialFieldManager.CreateSpatialFieldManager(view, 1);

					if (-1 != schemaId)
					{
						IList<int> results = sfm.GetRegisteredResults();
						if (!results.Contains(schemaId))
							schemaId = -1;
					}
					if (-1 == schemaId)
					{

						AnalysisResultSchema resultSchema1 = new AnalysisResultSchema(rnd.Next().ToString(), "Description");
						schemaId = sfm.RegisterResult(resultSchema1);
					}

					FaceArray faces = s.Faces;
					Transform trf = Transform.Identity;
					foreach (Face face in faces)
					{
						int idx = sfm.AddSpatialFieldPrimitive(face, trf);
						IList<UV> uvPts = new List<UV>();
						List<double> doubleList = new List<double>();
						IList<ValueAtPoint> valList = new List<ValueAtPoint>();
						BoundingBoxUV bb = face.GetBoundingBox();
						uvPts.Add(bb.Min);
						doubleList.Add(value);
						valList.Add(new ValueAtPoint(doubleList));

						FieldDomainPointsByUV pnts = new FieldDomainPointsByUV(uvPts);

						FieldValues vals = new FieldValues(valList);
						sfm.UpdateSpatialFieldPrimitive(idx, pnts, vals, schemaId);
					}
					transaction.Commit();
				}
			}
		}
		public static void CreateAVFDisplayStyle(Document doc, View view)
		{
			AnalysisDisplayColoredSurfaceSettings coloredSurfaceSettings = new AnalysisDisplayColoredSurfaceSettings();
			coloredSurfaceSettings.ShowGridLines = true;

			AnalysisDisplayColorSettings colorSettings = new AnalysisDisplayColorSettings();
			AnalysisDisplayLegendSettings legendSettings = new AnalysisDisplayLegendSettings();

			legendSettings.ShowLegend = false;
			var rnd = new Random();
			AnalysisDisplayStyle analysisDisplayStyle = AnalysisDisplayStyle.CreateAnalysisDisplayStyle(doc, "Paint Solid-" + rnd.Next(), coloredSurfaceSettings, colorSettings, legendSettings);
			view.AnalysisDisplayStyleId = analysisDisplayStyle.Id;
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
		public static void PrintExceptions()
		{
			string exInfo = "";
			foreach (string str in ExceptionFound)
			{
				exInfo += str + " " + Environment.NewLine + Environment.NewLine;
			}
			TaskDialog.Show("revit", "count: " + ExceptionFound.Count() + Environment.NewLine + exInfo);
		}
	}
}
