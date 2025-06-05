using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ticky.Base.Converters;

public class ColorToInt32Converter : ValueConverter<Color, int>
{
    public ColorToInt32Converter()
        : base(c => c.ToArgb(), v => Color.FromArgb(v)) { }
}
