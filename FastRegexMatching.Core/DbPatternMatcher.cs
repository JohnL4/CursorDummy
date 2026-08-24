using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace FastRegexMatching.Core;

public sealed class DbPatternMatcher : IPatternMatcher
{
    private readonly ILogger<DbPatternMatcher>? _logger;
    private volatile Regex? _activeRegex;

    public DbPatternMatcher(IEnumerable<string> initialPatterns, ILogger<DbPatternMatcher>? logger = null)
    {
        _logger = logger;
        Reload(initialPatterns);
    }

    public void Reload(IEnumerable<string> rawDbPatterns)
    {
        ArgumentNullException.ThrowIfNull(rawDbPatterns);

        var validPatterns = new List<string>();

        foreach (var pattern in rawDbPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            try
            {
                _ = new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
                validPatterns.Add(pattern);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Skipping invalid/unsupported regex from DB: {Pattern}", pattern);
            }
        }

        if (validPatterns.Count == 0)
        {
            _activeRegex = null;
            return;
        }

        string combined = string.Join("|", validPatterns.Select(p => $"(?:{p})"));

        try
        {
            var newRegex = new Regex(
                combined,
                RegexOptions.NonBacktracking | 
                RegexOptions.ExplicitCapture | 
                RegexOptions.CultureInvariant
            );

            _activeRegex = newRegex;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to compile combined NonBacktracking regex from DB patterns.");
            _activeRegex = null;
        }
    }

    public bool IsMatch(ReadOnlySpan<char> input)
    {
        var regex = _activeRegex;
        return regex is not null && regex.IsMatch(input);
    }

    public bool IsMatch(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IsMatch(input.AsSpan());
    }
}
