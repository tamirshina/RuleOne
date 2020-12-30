using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Analysis;
//using static RuleOne.Constants;
namespace RuleOne
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public static class SolidHelper
    {
		private static double ONE_INCH_BUFFER = UnitUtils.ConvertToInternalUnits(1d, DisplayUnitType.DUT_DECIMAL_INCHES);
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
				Constants.ExceptionFound.Add(exc.ToString() + Environment.NewLine);
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
				Constants.ExceptionFound.Add(exc.ToString() + Environment.NewLine);
				return null;
			}
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
				Constants.ExceptionFound.Add(exc.ToString() + "-TurnWallFaceToSolid-");
				return null;
			}
		}
		public static Solid TurnElementToItsLargestSolid(Element el, Transform linkedTransform)
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
				Constants.ExceptionFound.Add(exc.ToString());
			}
			return SolidUtils.CreateTransformed(solid, linkedTransform);
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
	}
}
