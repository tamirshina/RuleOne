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
		public static Solid TransformSolid(Transform targetTransform, Transform sourceTransform, Solid solid)
		{
			var transform = targetTransform.Multiply(sourceTransform);
			var solidInTargetModel = SolidUtils.CreateTransformed(solid, transform);
			return solidInTargetModel;
		}
		public static void ClearLists()
		{
			fireDumpers.Clear();
			whereIsFD.Clear();
			ExceptionFound.Clear();
			noFam.Clear();
			bbIsNull.Clear();
			ductInstulation.Clear();
		}
		public static List<RevitLinkInstance> GetAllLinked(Document doc)
		{
			List<RevitLinkInstance> linkedList = new List<RevitLinkInstance>();
			var models = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance));
			foreach (var m in models)
			{
				linkedList.Add(((RevitLinkInstance)m)); //m as RevitLinkInstance;
			}
			return linkedList;
		}
		public static Transform GetTransform(Document doc, string target)
		{
			var models = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance));
			foreach (var m in models)
			{
				var linkedModel = ((RevitLinkInstance)m); //m as RevitLinkInstance;
				var tempDoc = linkedModel.GetLinkDocument();
				if (tempDoc.Title.Contains(target))
				{
					return linkedModel.GetTotalTransform();
				}
			}
			return null;
		}
		public static Document GetDocument(Document doc, string target)
		{
			var models = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance));
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
		public static XYZ TransformPoint(XYZ point, Transform sourceTransform, Transform targetTransform)
		{
			var pointInHost = sourceTransform.OfPoint(point);
			var pointInTargetTransform = targetTransform.OfPoint(pointInHost);

			return pointInTargetTransform;
		}
		public static void PrintResults(string headline, List<Element> elList)
		{
			string info = "";

			foreach (Element ele in elList)
			{
				info += ele.Name + " " + ele.Id + "builtincat: " + (BuiltInCategory)ele.Category.Id.IntegerValue + Environment.NewLine;
			}
			TaskDialog.Show("revit", "Count: " + elList.Count() + Environment.NewLine + headline + "-"
							+ Environment.NewLine + info + Environment.NewLine);
		}
		public static void PrintResultsDuctsFamName(string headline, HashSet<Element> elList)
		{
			string info = "";

			string name = "";
			foreach (Element duct in elList)
			{
				if (duct != null)
				{
					name = duct.Name;
					info += name + " " + duct.Id + Environment.NewLine;
				}

			}
			TaskDialog.Show("revit", "Count: " + elList.Count() + Environment.NewLine + headline + "-"
							+ Environment.NewLine + info + Environment.NewLine);
		}
		public static bool AssertFrireWall(Element el)
		{
			Wall wall = el as Wall;
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
					foreach (string str in optionList)
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
							noFam.Add(duct);
							ductName = duct.DuctType.Name;
						}
						else
						{
							ductInstulation.Add(e);
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
		public static PlanarFace getIntersectionSolidRightFace(Solid intersectionSolid, Face wallFace)
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
		public static List<XYZ> getVerticesFromPlanarFace(PlanarFace planarFace)
		{
			var bufferInDirection = planarFace.FaceNormal.Multiply(1 / 3);
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
				Solid someSolid = CreateSolidFromVerticesWithCurveLoop((double)1 / 12, vertices, wallPlanarFace.FaceNormal,
					wallPlanarFace.GetEdgesAsCurveLoops(), linkedInstance);
				return someSolid;
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
						if (!(pf.FaceNormal == new XYZ(0, 0, 1)) || !(pf.FaceNormal == new XYZ(0, 0, -1)))
						{
							normalFaces.Add(pf);
						}
					}
				}
			}
			return normalFaces;
		}
		public static void PaintSolid(Document doc, Solid s, double value)
		{
			int schemaId = -1;
			var rnd = new Random();

			View view = doc.ActiveView;

			using (Transaction transaction = new Transaction(doc))
			{
				if (transaction.Start("Create model curves") == TransactionStatus.Started)
				{
					if (view.AnalysisDisplayStyleId == ElementId.InvalidElementId)
						CreateAVFDisplayStyle(doc, view);

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
		public static void PrintExceptions()
		{
			string exInfo = "";
			foreach (string str in ExceptionFound)
			{
				exInfo += str + " " + Environment.NewLine + Environment.NewLine;
			}
			TaskDialog.Show("revit", "count: " + ExceptionFound.Count() + exInfo);
		}
		public static IEnumerable<T> OrEmptyIfNull<T>(this IEnumerable<T> source)
		{
			return source ?? Enumerable.Empty<T>();
		}
		public static List<FireWallEl> GetAllFireWalls(Document doc)
		{
			List<FireWallEl> allFWEl = new List<FireWallEl>();

			IList<Element> linkedElemList = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks).OfClass(typeof(RevitLinkType)).ToElements();
			foreach (Element e in linkedElemList)
			{
				RevitLinkType linkType = e as RevitLinkType;

				foreach (Document linkedDoc in doc.Application.Documents)

				{
					if ((linkedDoc.Title + ".rvt").Equals(linkType.Name))

					{
						foreach (Element linkedEl in new FilteredElementCollector(linkedDoc)
							.OfClass(typeof(Wall)))
						{
							if (AssertFrireWall(linkedEl))
							{
								allFWEl.Add(new FireWallEl(linkedEl, linkedDoc));
							}
						}
					}
				}
			}
			return allFWEl;
		}
		public static void PrintFWList(List<FireWallEl> fWlist)
		{
			string exInfo = "";
			foreach (var el in fWlist)
			{
				exInfo += "wall name -" + el.FireWall.Name + " " + "wall ID- " + el.FireWall.Id.ToString() + Environment.NewLine +
					"fire rating- " + ((Wall)el.FireWall).WallType.get_Parameter(BuiltInParameter.DOOR_FIRE_RATING).AsString() + Environment.NewLine
					+ "doc title -" + el.WallDoc.Title + Environment.NewLine;
			}
			TaskDialog.Show("revit", "count: " + fWlist.Count() + Environment.NewLine + exInfo);
		}
		//old func 
		/*private List<Element> GetIntesectingWalls(Element elToIntersect, Document targetDoc, Transform targetTransform,
		Transform sourceTransform, Document doc)
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
								FindIntersectionPoint(doc, elToIntersect, e, sourceTransform);
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
			return wallsList;
		}*/
	}
}
