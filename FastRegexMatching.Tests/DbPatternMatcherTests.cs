using System;
using System.Collections.Generic;
using System.Linq;
using FastRegexMatching.Core;
using Xunit;

namespace FastRegexMatching.Tests;

public class DbPatternMatcherTests
{
    [Fact]
    public void MatchesValidPatterns()
    {
        var patterns = new[]
        {
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
            @"\b\d{3}-\d{2}-\d{4}\b"
        };

        var matcher = new DbPatternMatcher(patterns);

        Assert.True(matcher.IsMatch("Email: user@example.com"));
        Assert.True(matcher.IsMatch("SSN: 000-11-2222"));
        Assert.False(matcher.IsMatch("Random text without matches"));
    }

    [Fact]
    public void SkipsInvalidAndUnsupportedPatterns()
    {
        var patterns = new[]
        {
            "(invalid[regex",
            @"(?<=lookbehind_)\d+", // Unsupported by NonBacktracking, will be skipped
            @"valid_\d+"
        };

        var matcher = new DbPatternMatcher(patterns);

        Assert.True(matcher.IsMatch("valid_456"));
        Assert.False(matcher.IsMatch("lookbehind_456"));
    }

    [Fact]
    public void ReloadsCorrectly()
    {
        var matcher = new DbPatternMatcher(new[] { "first_pattern" });
        Assert.True(matcher.IsMatch("first_pattern"));
        Assert.False(matcher.IsMatch("second_pattern"));

        matcher.Reload(new[] { "second_pattern" });
        Assert.False(matcher.IsMatch("first_pattern"));
        Assert.True(matcher.IsMatch("second_pattern"));
    }
}
