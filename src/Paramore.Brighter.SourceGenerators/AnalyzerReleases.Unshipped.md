; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
BRGEN001 | Brighter | Error | Brighter registration method must be partial
BRGEN002 | Brighter | Error | Brighter registration method must be static
BRGEN003 | Brighter | Error | Brighter registration method must return IBrighterBuilder
BRGEN004 | Brighter | Error | Brighter registration method must take a single IBrighterBuilder parameter
BRGEN005 | Brighter | Warning | Generic message mappers and transforms are not registered
BRGEN006 | Brighter | Warning | Types nested in an open generic type are not registered
BRGEN007 | Brighter | Info | Auto-registration suppressed by a manual registration method
