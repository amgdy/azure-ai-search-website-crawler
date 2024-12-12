using Microsoft.ML.Tokenizers;

namespace AzureAiSearchWebsiteCrawler.Utilities;

public static class TextSplitter
{
    /// <summary>
    /// Splits the given text into multiple segments each of up to maxChars length,
    /// with a specified overlap percentage between consecutive segments,
    /// attempting not to split words across chunks.
    /// </summary>
    /// <param name="text">The input text to split.</param>
    /// <param name="maxChars">The maximum length of each segment.</param>
    /// <param name="overlapPercentage">
    /// A value between 0.0 and 1.0 indicating how much of each segment should overlap with the next.
    /// For example, 0.1 means 10% overlap.
    /// </param>
    /// <returns>A list of string segments (non-empty and non-whitespace).</returns>
    public static List<string> SplitTextWithOverlapNoWordSplit(string text, int maxChars, double overlapPercentage)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));

        if (maxChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChars), "maxChars must be greater than zero.");

        if (overlapPercentage < 0.0 || overlapPercentage >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(overlapPercentage), "overlapPercentage must be between 0.0 and 1.0 (exclusive).");

        var result = new List<string>();
        int length = text.Length;
        int overlapCount = (int)(maxChars * overlapPercentage);

        int currentStart = 0;
        while (currentStart < length)
        {
            int remaining = length - currentStart;
            int currentLength = remaining < maxChars ? remaining : maxChars;

            // Attempt to avoid splitting in the middle of a word.
            int adjustedLength = FindWordBoundary(text, currentStart, currentLength);
            currentLength = adjustedLength;

            string chunk = text.Substring(currentStart, currentLength);

            // Ensure we don't add empty or whitespace-only chunks
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                result.Add(chunk);
            }

            if (currentStart + currentLength >= length)
                break;

            currentStart = currentStart + currentLength - overlapCount;
        }

        return result;
    }

    /// <summary>
    /// Overload that calculates maxChars from maxTokens using CountTokens and applies a safety margin.
    /// </summary>
    /// <param name="text">The input text to split.</param>
    /// <param name="maxTokens">The desired maximum number of tokens per segment.</param>
    /// <param name="overlapPercentage">The overlap percentage for segments.</param>
    /// <param name="tokenizer">Tokenizer to count tokens.</param>
    /// <param name="safetyMargin">A factor less than or equal to 1.0 to ensure we don't exceed token targets.</param>
    /// <returns>A list of string segments (non-empty and non-whitespace).</returns>
    public static List<string> SplitTextWithOverlapNoWordSplit(
        string text,
        int maxTokens,
        double overlapPercentage,
        Tokenizer tokenizer,
        double safetyMargin = 0.9)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));

        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "maxTokens must be greater than zero.");

        if (overlapPercentage < 0.0 || overlapPercentage >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(overlapPercentage), "overlapPercentage must be between 0.0 and 1.0 (exclusive).");

        if (safetyMargin <= 0 || safetyMargin > 1)
            throw new ArgumentOutOfRangeException(nameof(safetyMargin), "safetyMargin should be between (0,1].");

        int totalTokens = tokenizer.CountTokens(text);
        if (totalTokens == 0)
        {
            // If no tokens, just return the entire text if it's not empty whitespace
            return string.IsNullOrWhiteSpace(text) ? new List<string>() : new List<string> { text };
        }

        // Calculate ratio of tokens to characters
        double ratio = (double)maxTokens / totalTokens;

        // Derive maxChars from ratio and safety margin
        int maxChars = (int)(text.Length * ratio * safetyMargin);

        if (maxChars <= 0)
        {
            // If calculation leads to zero or negative (very small ratio), just pick a small positive number
            maxChars = Math.Min(text.Length, 50); // Arbitrary fallback
        }

        return SplitTextWithOverlapNoWordSplit(text, maxChars, overlapPercentage);
    }

    /// <summary>
    /// Finds a suitable word boundary at or before currentStart + currentLength,
    /// so as not to split inside a word. Returns the adjusted length.
    /// </summary>
    private static int FindWordBoundary(string text, int currentStart, int currentLength)
    {
        int endIndex = currentStart + currentLength - 1;
        char[] separators = { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':' };

        for (int i = endIndex; i > currentStart; i--)
        {
            char c = text[i];
            if (Array.IndexOf(separators, c) >= 0)
            {
                return i - currentStart + 1;
            }
        }

        return currentLength;
    }
}