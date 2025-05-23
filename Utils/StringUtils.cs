namespace Assistant.Utils;

public static class StringUtils
{
    public static string Truncate(this string input, int maxLength, string? suffix)
    {
        if (input.Length <= maxLength)
            return input;

        return input[..maxLength] + suffix;
    }
}
