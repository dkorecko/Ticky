using System.Drawing;

namespace Ticky.Internal.Helpers;

public static class ColorHelper
{
    public static string ToColorInputValue(this Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
