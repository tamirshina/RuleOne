using System;

namespace RuleOne
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autodesk.Revit.Attributes;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.DB.Analysis;
    using Autodesk.Revit.DB.Mechanical;
    using Autodesk.Revit.UI;
	using static RuleOne.Helper;
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
			PrintExceptions();
			ClearLists();
		}
		public void PrintExceptions()
		{
			string exInfo = "";
			foreach (string str in ExceptionFound)
			{
				exInfo += str + " " + Environment.NewLine + Environment.NewLine;
			}
			TaskDialog.Show("revit", "count: " + ExceptionFound.Count() + exInfo);
		}
		public void ClearLists()
		{
			fireDumpers.Clear();
			whereIsFD.Clear();
			ExceptionFound.Clear();

			bbIsNull.Clear();
			noFamExc.Clear();
			noConn.Clear();
			solidIntersects.Clear();
		}
		List<Element> bbIsNull = new List<Element>();
		List<string> optionList = new List<string>{"Firecase Column", "Fire Rated", "Fireline", "Fire Rated Wall",
			"Fire Rated Partition Wall","Fire Rated Partition", "Fire Rated Shaft", "Fire Rated Barrier", "Fire Wall",
			"Fire Partition Wall", "Fire Partition ","Fire Shaft", "Fire Barrier", "Fire Firewall", "FR Wall",
			"FR Rated Partition Wall","FR Partition", "FR Shaft", "FR Barrier", "F/R Wall", "F/R Partition Wall",
			"F/R Partition", "F/R Shaft","F/R Barrier", "FireWall", "FR", "F/R","Fire Rated" };
		List<String> ExceptionFound = new List<string>();
		List<string> noFamExc = new List<string>();
		List<Element> noConn = new List<Element>();
		List<Element> fireDumpers = new List<Element>();
		List<Element> solidIntersects = new List<Element>();
		List<Element> whereIsFD = new List<Element>();

		List<BuiltInCategory> ductsBuiltInCats = new List<BuiltInCategory>(){BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_DuctSystem, BuiltInCategory.OST_DuctFitting,
			BuiltInCategory.OST_DuctAccessory, BuiltInCategory.OST_FlexDuctTags, BuiltInCategory.OST_DuctTags};
		private readonly string[] optionalFamiliesNames =
		{
			"fd", "firedamper", "firesmokedamper", "damperrectangular", "br+adamper", "smokedamper", "firesmoke",
			"fsd"
		};
		private readonly string[] optionalFamiliesNamesToExclude = { "volumecontroldamper", "balanc" };

		private Transform getTransform(Document doc, string target)
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
		private Document getDocument(Document doc, string target)
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
		public static XYZ TransformPoint(XYZ point, Transform sourceTransform, Transform targetTransform)
		{
			var pointInHost = sourceTransform.OfPoint(point);
			var pointInTargetTransform = targetTransform.OfPoint(pointInHost);

			return pointInTargetTransform;
		}
		private void PrintResults(string headline, List<Element> elList)
		{
			string info = "";

			foreach (Element ele in elList)
			{
				info += ele.Name + " " + "builtincat: " + (BuiltInCategory)ele.Category.Id.IntegerValue + Environment.NewLine;
			}
			TaskDialog.Show("revit", "Count: " + elList.Count() + Environment.NewLine + headline + "-"
							+ Environment.NewLine + info + Environment.NewLine);
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
						if (Helper.AssertFrireWall(e))
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
		private bool AssertFireDumper(Element e)
		{
			try
			{
				foreach (string str in optionalFamiliesNames)
				{
                    FamilyInstance fInstance = e as FamilyInstance;
                    FamilySymbol FType = fInstance.Symbol;
                    Family Fam = FType.Family;

					foreach (string excludeStr in optionalFamiliesNamesToExclude)
					{
						if (Fam.Name.ToLower().Contains(excludeStr))
						{
							return false;
						}
						else 
						{
							if (Fam.Name.ToLower().Contains(str))
							{
								return true;
							}
						}
					}
				}
			}
			catch (Exception exc)
			{
				noFamExc.Add(exc.ToString() + " -" + e.Id);
				return false;
			}
			return false;
		}
		private bool isHrizontal(Element el)
		{
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
					bool conn = duct.ConnectorManager.Connectors.IsEmpty;
					if (conn == true)
					{
						noConn.Add(el);
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

			Solid finalSolid = null;
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


						finalSolid = CreateSolidFromVertices((double)(8 / 12 + wall.Width), getVerticesFromPlanarFace(solidPlanarFace), solidPlanarFace.FaceNormal.Negate());
						//PaintSolid(doc, finalSolid, 1);
						FilteredElementCollector collector = new FilteredElementCollector(getDocument(doc, "MEP"));

						collector.WherePasses(new ElementIntersectsSolidFilter(finalSolid));

						foreach (Element element in collector)
						{
							solidIntersects.Add(element);
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
/*		public static IEnumerable<T> OrEmptyIfNull<T>(this IEnumerable<T> source)
		{
			return source ?? Enumerable.Empty<T>();
		}*/
		public void PaintSolid(Document doc, Solid s, double value)
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
		private void CreateAVFDisplayStyle(Document doc, View view)
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
	}
}