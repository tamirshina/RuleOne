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
		
		public static void ClearLists()
		{
			fireDumpers.Clear();
			whereIsFD.Clear();
			ExceptionFound.Clear();

			bbIsNull.Clear();
		}
		public static Transform getTransform(Document doc, string target)
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
		public static Document getDocument(Document doc, string target)
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
				info += ele.Name + " " + "builtincat: " + (BuiltInCategory)ele.Category.Id.IntegerValue + Environment.NewLine;
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
				noFam.Add(e);
				return false;
			}
			return false;
		}
		public static bool isHrizontal(Element el)
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
	}
}
