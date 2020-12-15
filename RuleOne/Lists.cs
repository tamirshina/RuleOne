using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;

namespace RuleOne
{
	public static class Lists
    {
		public static List<String> ExceptionFound = new List<string>();
		public static List<String> famNames = new List<string>();
		public static HashSet<Duct> noFam = new HashSet<Duct>();
		public static HashSet<Element> bbIsNull = new HashSet<Element>();
		public static HashSet<Element> ductInstulation = new HashSet<Element>();
		public static List<Element> fireDumpers = new List<Element>();
		public static List<Element> fireDumpersFromIntersection = new List<Element>();
		public static HashSet<Element> whereIsFD = new HashSet<Element>();
		public static List<Element> structuralFraming = new List<Element>();
		public static HashSet<Element> generalElList = new HashSet<Element>();
		public static List<BuiltInCategory> allCatagories = new List<BuiltInCategory>();

		public static List<string> optionList = new List<string>{"Firecase Column", "Fire Rated", "Fireline", "Fire Rated Wall",
			"Fire Rated Partition Wall","Fire Rated Partition", "Fire Rated Shaft", "Fire Rated Barrier", "Fire Wall",
			"Fire Partition Wall", "Fire Partition ","Fire Shaft", "Fire Barrier", "Fire Firewall", "FR Wall",
			"FR Rated Partition Wall","FR Partition", "FR Shaft", "FR Barrier", "F/R Wall", "F/R Partition Wall",
			"F/R Partition", "F/R Shaft","F/R Barrier", "FireWall", "FR", "F/R","Fire Rated" };

		static public List<BuiltInCategory> ductsBuiltInCats = new List<BuiltInCategory>(){BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_DuctFitting,
			BuiltInCategory.OST_DuctAccessory, BuiltInCategory.OST_FlexDuctTags, BuiltInCategory.OST_DuctTags, BuiltInCategory.OST_DuctInsulations};
		static public List<BuiltInCategory> customBuiltInCats = new List<BuiltInCategory>(){BuiltInCategory.OST_DuctCurves};
		static public List<BuiltInCategory> filterBuiltInCats = new List<BuiltInCategory>() { BuiltInCategory.OST_Walls, BuiltInCategory.OST_StructuralFraming };
		static public List<BuiltInCategory> builtInCatsIsEnd = new List<BuiltInCategory>() { BuiltInCategory.OST_Walls, BuiltInCategory.OST_DuctTerminal };
		static public readonly string[] optionalFamiliesNames =
		{
			"fd", "firedamper", "firesmokedamper", "damperrectangular", "br+adamper", "smokedamper", "firesmoke",
			"fsd"
		};
		static public readonly string[] optionalFamiliesNamesToExclude = { "volumecontroldamper", "balanc" };
	}

}
