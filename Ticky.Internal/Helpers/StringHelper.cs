using System.Text;

namespace Ticky.Internal.Helpers;

public static class StringHelper
{
    public static string ToFriendlyName(this string str)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < str.Length; i++)
        {
            if (i != 0 && char.IsUpper(str[i]))
            {
                sb.Append(' ');
                sb.Append(char.ToLower(str[i]));
            }
            else
                sb.Append(str[i]);
        }

        return sb.ToString();
    }
}
