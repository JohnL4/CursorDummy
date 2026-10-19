using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FastRegexMatching.Core;
using Xunit;

namespace FastRegexMatching.Tests;

public class HybridDbPatternMatcherTests
{
    [Fact]
    public void MatchesNonBacktrackingPatterns()
    {
        var patterns = new List<string>
        {
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", // Email
            @"\b\d{3}-\d{2}-\d{4}\b",                                // SSN
            @"https?://[^\s/$.?#].[^\s]*"                            // URL
        };

        var matcher = new HybridDbPatternMatcher(patterns);

        Assert.True(matcher.IsMatch("Contact us at support@example.com for help."));
        Assert.True(matcher.IsMatch("SSN is 123-45-6789 confidential"));
        Assert.True(matcher.IsMatch("Visit https://example.org today"));
        Assert.False(matcher.IsMatch("Plain text with no sensitive info."));
    }

    [Fact]
    public void MatchesLookaroundFallbackPatterns()
    {
        var patterns = new List<string>
        {
            @"\btoken_[a-z0-9]+\b",
            @"(?<=secret_)\d+" // Lookbehind requires backtracking engine in .NET
        };

        var matcher = new HybridDbPatternMatcher(patterns);

        Assert.True(matcher.IsMatch("token_abc123"));
        Assert.True(matcher.IsMatch("secret_98765"));
        Assert.False(matcher.IsMatch("other_ABCDE"));
    }

    [Fact]
    public void HandlesLargeNumberOfPatterns()
    {
        // 500 distinct regex patterns
        var patterns = Enumerable.Range(1, 500)
            .Select(i => $@"error_code_{i}_\d{{2,4}}")
            .ToList();

        var matcher = new HybridDbPatternMatcher(patterns);

        Assert.True(matcher.IsMatch("System alert: error_code_1_456 occurred"));
        Assert.True(matcher.IsMatch("System alert: error_code_250_9999 occurred"));
        Assert.True(matcher.IsMatch("System alert: error_code_500_12 occurred"));
        Assert.False(matcher.IsMatch("System alert: error_code_501_12 occurred"));
        Assert.False(matcher.IsMatch("System alert: error_code_0_12 occurred"));
    }

    [Fact]
    public void ReloadsPatternsSuccessfully()
    {
        var matcher = new HybridDbPatternMatcher(new[] { @"alpha_\d+" });

        Assert.True(matcher.IsMatch("alpha_123"));
        Assert.False(matcher.IsMatch("beta_123"));

        matcher.Reload(new[] { @"beta_\d+" });

        Assert.False(matcher.IsMatch("alpha_123"));
        Assert.True(matcher.IsMatch("beta_123"));
    }

    [Fact]
    public void ToleratesInvalidRegexesInDb()
    {
        var patterns = new List<string>
        {
            "(unclosed_parenthesis_[0-9]+", // Invalid syntax
            @"[a-z]+_\d+"                   // Valid
        };

        var matcher = new HybridDbPatternMatcher(patterns);

        Assert.True(matcher.IsMatch("valid_123"));
        Assert.False(matcher.IsMatch("INVALID"));
    }

    [Fact]
    public void IsThreadSafeAcrossConcurrentThreads()
    {
        var patterns = Enumerable.Range(1, 100)
            .Select(i => $@"user_id_{i}_\w+")
            .Concat(new[] { @"(?<=key_)\d{4}" }) // mix in lookbehind
            .ToList();

        var matcher = new HybridDbPatternMatcher(patterns);

        Parallel.For(0, 10_000, i =>
        {
            int id = (i % 100) + 1;
            string matchInput = $"Event log for user_id_{id}_active at timestamp";
            string nonMatchInput = "Event log for guest_user at timestamp";

            Assert.True(matcher.IsMatch(matchInput.AsSpan()));
            Assert.False(matcher.IsMatch(nonMatchInput.AsSpan()));
        });
    }

    [Fact]
    public void HandlesEmptyPatternsGracefully()
    {
        var matcher = new HybridDbPatternMatcher(Array.Empty<string>());
        Assert.False(matcher.IsMatch("any input text"));
        Assert.False(matcher.IsMatch("".AsSpan()));
    }
}
