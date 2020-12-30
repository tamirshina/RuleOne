using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace RuleOne
{
    class IdentificationHelper
    {
		public static bool IsMetalBeam(Element el)
		{
			BuiltInCategory structuralBeam = BuiltInCategory.OST_StructuralFraming;
			BuiltInCategory elementCategory = (BuiltInCategory)el.Category.Id.IntegerValue;

			return structuralBeam.Equals(elementCategory);
		}
		public static bool IsFireRatedWall(Element element)
		{
			try
			{
				if (element is Wall wall)
				{
					if (wall.WallType.Function.ToString() == "Interior" || wall.WallType.Function.ToString() != "Exterior")
					{
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
							foreach (string str in Constants.fireRatedNameOptions)
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
				Constants.ExceptionFound.Add(exc.ToString());
				return false;
			}
		}
		public static bool IsFireDumper(Element e)
		{
			try
			{
				string ductName = "";
				foreach (string str in Constants.optionalFamiliesNames)
				{
					if (e is FamilyInstance fInstance)
					{
						FamilySymbol familySymbol = fInstance.Symbol;
						Family Family = familySymbol.Family;
						ductName = Family.Name;
					}
					else
					{
						if (e is Duct duct)
						{
							ductName = duct.DuctType.Name;
						}
					}
					foreach (string excludeStr in Constants.optionalFamiliesNamesToExclude)
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
				Constants.ExceptionFound.Add(exc.ToString());
				return false;
			}
			return false;
		}
		public static bool IsHorizontal(Element element)
		{
			try
			{
				if (element is Duct duct)
				{
					Parameter param = duct.get_Parameter(BuiltInParameter.RBS_START_OFFSET_PARAM);
					if (param != null)
					{
						Parameter param2 = duct.get_Parameter(BuiltInParameter.RBS_END_OFFSET_PARAM);
						if (param2 != null)
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
				Constants.ExceptionFound.Add(exc.ToString());
				return true;
			}
			return true;
		}

		public static PlanarFace GetWallAndIntersectionSolidJoinedPlananrFace(Solid intersectionSolid, Face wallFace)
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
					Constants.ExceptionFound.Add(exc.ToString());
					continue;
				}
			}
			return null;
		}
	}
}
