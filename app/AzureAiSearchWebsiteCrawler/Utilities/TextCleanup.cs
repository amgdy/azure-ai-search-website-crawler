using System.Text.RegularExpressions;

namespace AzureAiSearchWebsiteCrawler.Utilities;

internal static partial class TextCleanup
{
    /// <summary>
    /// Cleans up text by removing multiple newlines, spaces, and hyphens
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    internal static string Cleanup(string text)
    {
        // Match two or more newlines (including \n, \r, and \r\n) and replace them with one new line
        string output = MultipleNewlinesRegex().Replace(text, Environment.NewLine);

        // Match multiple newlines followed by spaces and replace them with one new line
        output = NewlinesFollowedBySpacesRegex().Replace(output, Environment.NewLine);

        // Match two or more spaces that are not newlines and replace them with one space
        output = MultipleSpacesRegex().Replace(output, " ");

        // Match two or more hyphens and replace them with two hyphens
        output = MultipleHyphensRegex().Replace(output, "--");

        return output.Trim();
    }

    /// <summary>
    /// Matches multiple newlines followed by spaces
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"(\r\n|\r|\n)+\s+")]
    private static partial Regex NewlinesFollowedBySpacesRegex();

    /// <summary>
    /// Matches two or more newlines (including \n, \r, and \r\n)
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"(\r\n|\r|\n){2,}")]
    private static partial Regex MultipleNewlinesRegex();

    /// <summary>
    /// Matches two or more spaces that are not newlines
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"[^\S\n]{2,}")]
    private static partial Regex MultipleSpacesRegex();

    /// <summary>
    /// Matches two or more spaces that are not newlines
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleHyphensRegex();
}
