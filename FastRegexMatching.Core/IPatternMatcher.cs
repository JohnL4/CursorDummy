using System;
using System.Collections.Generic;

namespace FastRegexMatching.Core;

public interface IPatternMatcher
{
    bool IsMatch(ReadOnlySpan<char> input);
    bool IsMatch(string input);
    void Reload(IEnumerable<string> rawDbPatterns);
}
