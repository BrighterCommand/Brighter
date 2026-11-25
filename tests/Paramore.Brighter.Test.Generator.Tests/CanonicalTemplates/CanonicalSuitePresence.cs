using System.Collections.Generic;
using System.IO;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

internal static class CanonicalSuitePresence
{
    internal static IReadOnlyList<string> FindMissingCanonicalFiles(
        IEnumerable<string> generatedDirs,
        string variant)
    {
        var missing = new List<string>();

        foreach (var dir in generatedDirs)
        {
            foreach (var templateName in CanonicalBehaviours.TEMPLATE_FR_COLUMNS.Keys)
            {
                var filePath = Path.Combine(dir, $"{templateName}.cs");
                if (!File.Exists(filePath))
                    missing.Add($"  {variant}: {filePath}");
            }
        }

        return missing;
    }
}
