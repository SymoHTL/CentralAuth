namespace Domain.Extensions;

public static class StringExtensions {
    public static bool IsNullEmptyOrWhiteSpace(this string? value) =>
        string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value);


    public static string EscapeToUrl(this string value) =>
        Uri.EscapeDataString(value);

    public static string UnescapeFromUrl(this string value) =>
        Uri.UnescapeDataString(value);
}