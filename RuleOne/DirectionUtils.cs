
using Autodesk.Revit.DB;

namespace RuleOne
{
    public static class DirectionUtils
    {
        private const double MIN_SLOPE = 0.3;
        public static XYZ TopDirection => XYZ.BasisZ;
        public static XYZ BottomDirection => XYZ.BasisZ.Negate();
        public static bool VectorDirectionUpwards(XYZ vector)
        {
            double horizontalLength = vector.X * vector.X + vector.Y * vector.Y;
            double verticalLength = vector.Z * vector.Z;
            return 0 < vector.Z && ((horizontalLength == 0 && verticalLength != 0) || MIN_SLOPE < verticalLength / horizontalLength);
        }
        public static bool VectorDirectionDownwards(XYZ vector)
        {
            double horizontalLength = vector.X * vector.X + vector.Y * vector.Y;
            double verticalLength = vector.Z * vector.Z;
            return 0 > vector.Z && ((horizontalLength == 0 && verticalLength != 0) || MIN_SLOPE < verticalLength / horizontalLength);
        }
    }
}
