# FastRegexMatching (.NET 8)

High-performance regular expression matching library for comparing strings against hundreds of dynamic regular expressions loaded at runtime (e.g., from a database).

## Features

- **Single-Pass DFA State Machine (`RegexOptions.NonBacktracking`)**: Combines hundreds of regexes into a single alternation `(?:p1)|(?:p2)|...` evaluated in $O(L)$ time proportional only to the length of the input string.
- **Hybrid Two-Tier Architecture (`HybridDbPatternMatcher`)**:
  - **Tier 1 (Fast Path)**: Combines all DFA-compatible patterns into one non-backtracking state machine ($0$ allocations, early termination).
  - **Tier 2 (Fallback Loop)**: Automatically handles patterns requiring lookarounds or backreferences using compiled backtracking regexes guarded by configurable timeouts (`RegexMatchTimeoutException`).
- **Thread-Safe & Lock-Free**: Immutable `Regex` instances enable safe concurrent evaluations across any number of worker threads. Rebuilds and atomically swaps state machines during dynamic DB reloads without blocking active traffic.
- **Zero Allocations on Match**: Implements `IsMatch(ReadOnlySpan<char>)` for zero heap allocations per check.

## Usage

```csharp
using FastRegexMatching.Core;

// Load patterns from DB
var dbPatterns = new List<string>
{
    @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
    @"\b\d{3}-\d{2}-\d{4}\b",
    @"(?<=secret_)\d+" // Automatically routed to Tier 2 fallback
};

var matcher = new HybridDbPatternMatcher(dbPatterns);

// Fast, zero-allocation match
bool isMatch = matcher.IsMatch("User registered with email support@example.com");

// Live reload from DB with zero downtime
matcher.Reload(updatedPatternsFromDb);
```

## Running Tests

```bash
dotnet test
```
