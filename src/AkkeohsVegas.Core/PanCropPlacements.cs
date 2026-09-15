using System;

namespace AkkeohsVegas.Core
{

    public static class PanCropPlacements
    {
        public const string Default = "Default";
        public const string LowerThird = "Lower Third";
        public const string BottomCenter = "Bottom Center";
        public const string BottomLeft = "Bottom Left";
        public const string BottomRight = "Bottom Right";
        public const string TopCenter = "Top Center";
        public const string TopLeft = "Top Left";
        public const string TopRight = "Top Right";
        public const string Center = "Center";

        public static readonly string[] All =
        {
            Default,
            LowerThird,
            BottomCenter,
            BottomLeft,
            BottomRight,
            TopCenter,
            TopLeft,
            TopRight,
            Center
        };

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LowerThird;

            foreach (string name in All)
            {
                if (string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            return LowerThird;
        }
    }
}
