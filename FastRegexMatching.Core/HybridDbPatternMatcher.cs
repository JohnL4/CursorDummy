using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace FastRegexMatching.Core;

public sealed class HybridDbPatternMatcher : IPatternMatcher
{
    private readonly ILogger<HybridDbPatternMatcher>? _logger;
    private readonly TimeSpan _backtrackTimeout;

    // Tier 1: Single combined non-backtracking regex
    private volatile Regex? _tier1CombinedRegex;

    // Tier 2: Fallback list of compiled backtracking regexes (thread-safe)
    private volatile Regex[] _tier2FallbackRegexes = Array.Empty<Regex>();

    public HybridDbPatternMatcher(
        IEnumerable<string> rawDbPatterns,
        ILogger<HybridDbPatternMatcher>? logger = null,
        TimeSpan? backtrackTimeout = null)
    {
        _logger = logger;
        _backtrackTimeout = backtrackTimeout ?? TimeSpan.FromMilliseconds(200);
        Reload(rawDbPatterns);
    }

    public void Reload(IEnumerable<string> rawDbPatterns)
    {
        ArgumentNullException.ThrowIfNull(rawDbPatterns);

        var nonBacktrackingList = new List<string>();
        var fallbackList = new List<Regex>();

        foreach (var pattern in rawDbPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            // Attempt 1: Check if compatible with NonBacktracking
            try
            {
                _ = new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
                nonBacktrackingList.Add(pattern);
                continue;
            }
            catch (NotSupportedException)
            {
                // Pattern uses lookarounds, backreferences, or features unsupported by DFA; route to Tier 2
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Invalid regex syntax in DB: {Pattern}", pattern);
                continue;
            }

            // Attempt 2: Compile as backtracking with a timeout guard
            try
            {
                var compiledRegex = new Regex(
                    pattern,
                    RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
                    _backtrackTimeout
                );
                fallbackList.Add(compiledRegex);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to compile backtracking regex in DB: {Pattern}", pattern);
            }
        }

        // Build Tier 1 combined regex
        if (nonBacktrackingList.Count > 0)
        {
            string combined = string.Join("|", nonBacktrackingList.Select(p => $"(?:{p})"));
            try
            {
                _tier1CombinedRegex = new Regex(
                    combined,
                    RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant
                );
            }
            catch (NotSupportedException ex)
            {
                // If combined automaton size exceeds NonBacktracking threshold, fallback remaining to Tier 2
                _logger?.LogWarning(ex, "Combined NonBacktracking regex exceeded automaton limit. Falling back individual patterns to backtracking engine.");
                _tier1CombinedRegex = null;

                foreach (var pattern in nonBacktrackingList)
                {
                    try
                    {
                        var compiledRegex = new Regex(
                            pattern,
                            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
                            _backtrackTimeout
                        );
                        fallbackList.Add(compiledRegex);
                    }
                    catch (Exception compileEx)
                    {
                        _logger?.LogWarning(compileEx, "Failed to compile fallback regex: {Pattern}", pattern);
                    }
                }
            }
        }
        else
        {
            _tier1CombinedRegex = null;
        }

        // Atomic swap of Tier 2
        _tier2FallbackRegexes = fallbackList.ToArray();
    }

    public bool IsMatch(ReadOnlySpan<char> input)
    {
        // 1. Fast Path: Check hundreds of patterns in one pass using DFA
        var tier1 = _tier1CombinedRegex;
        if (tier1 is not null && tier1.IsMatch(input))
        {
            return true;
        }

        // 2. Slow Path: Fallback loop only if Tier 1 misses
        var tier2 = _tier2FallbackRegexes;
        for (int i = 0; i < tier2.Length; i++)
        {
            try
            {
                if (tier2[i].IsMatch(input))
                {
                    return true;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                _logger?.LogWarning("Regex match timed out for a fallback pattern.");
            }
        }

        return false;
    }

    public bool IsMatch(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return IsMatch(input.AsSpan());
    }
}
