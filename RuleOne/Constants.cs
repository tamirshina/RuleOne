using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RuleOne
{
	public static class Constants
	{
		public static int tamir = 1; 
		public const int NO_CHANGE_BUFFER = 1;
		public static readonly double EIGHT_INCH_BUFFER = UnitUtils.ConvertToInternalUnits(8d, DisplayUnitType.DUT_DECIMAL_INCHES);
		public static readonly double SIX_INCH_BUFFER = UnitUtils.ConvertToInternalUnits(6d, DisplayUnitType.DUT_DECIMAL_INCHES);
		public static readonly double FOUR_INCH_BUFFER = UnitUtils.ConvertToInternalUnits(4d, DisplayUnitType.DUT_DECIMAL_INCHES);
		public static List<String> ExceptionFound = new List<string>();

		public static List<BuiltInCategory> allCatagories = new List<BuiltInCategory>();

		public static List<string> fireRatedNameOptions = new List<string>{"Firecase Column", "Fire Rated", "Fireline", "Fire Rated Wall",
			"Fire Rated Partition Wall","Fire Rated Partition", "Fire Rated Shaft", "Fire Rated Barrier", "Fire Wall",
			"Fire Partition Wall", "Fire Partition ","Fire Shaft", "Fire Barrier", "Fire Firewall", "FR Wall",
			"FR Rated Partition Wall","FR Partition", "FR Shaft", "FR Barrier", "F/R Wall", "F/R Partition Wall",
			"F/R Partition", "F/R Shaft","F/R Barrier", "FireWall", "FR", "F/R","Fire Rated" };

		static public List<BuiltInCategory> ductCategories = new List<BuiltInCategory>(){BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_DuctFitting,
			BuiltInCategory.OST_DuctAccessory, BuiltInCategory.OST_FlexDuctTags, BuiltInCategory.OST_DuctTags, BuiltInCategory.OST_DuctInsulations};
		static public List<BuiltInCategory> customBuiltInCats = new List<BuiltInCategory>(){BuiltInCategory.OST_DuctCurves};
		static public List<BuiltInCategory> intersectionElementsCatagories = new List<BuiltInCategory>() { BuiltInCategory.OST_Walls, BuiltInCategory.OST_StructuralFraming };
		static public List<BuiltInCategory> builtInCatsIsEnd = new List<BuiltInCategory>() { BuiltInCategory.OST_Walls, BuiltInCategory.OST_DuctTerminal };
		static public readonly string[] optionalFamiliesNames =
		{
			"fd", "firedamper", "firesmokedamper", "damperrectangular", "br+adamper", "smokedamper", "firesmoke",
			"fsd"
		};
		static public readonly string[] optionalFamiliesNamesToExclude = { "volumecontroldamper", "balanc" };
	}

}
