using UiUxGenomeLab.Domain;

namespace UiUxGenomeLab.Services;

/*
 * Genetic optimizer (selection/crossover/mutation).
 * Should also let OpenAI propose mutations by feeding the elite specs back into a GenerateNextGenerationAsync method, but this class gives a deterministic, cheap baseline.
*/

public sealed class GeneticOptimizer
{
    private readonly Random _rng = new();

    public IReadOnlyList<UiUxDesignCandidate> SelectElite(
        IReadOnlyList<UiUxDesignCandidate> candidates,
        int eliteCount)
    {
        return candidates
            .OrderByDescending(c => c.OverallFitness)
            .Take(eliteCount)
            .ToArray();
    }

    public IReadOnlyList<UiUxDesignCandidate> MutateAndCrossover(
        IReadOnlyList<UiUxDesignCandidate> parents,
        int targetPopulationSize,
        int generationIndex)
    {
        var next = new List<UiUxDesignCandidate>(targetPopulationSize);
        var parentArray = parents.ToArray();

        while (next.Count < targetPopulationSize)
        {
            var a = parentArray[_rng.Next(parentArray.Length)];
            var b = parentArray[_rng.Next(parentArray.Length)];

            var child = CreateChild(a, b, generationIndex, next.Count);
            next.Add(child);
        }

        return next;
    }

    private UiUxDesignCandidate CreateChild(
        UiUxDesignCandidate a,
        UiUxDesignCandidate b,
        int gen,
        int index)
    {
        UiUxDesignSpec Mutate(UiUxDesignSpec spec)
        {
            // Very simple mutation – you can make this smarter with LLM assistance.
            string MutateField(string value)
            {
                if (_rng.NextDouble() < 0.15)
                {
                    // e.g., tweak a keyword
                    return value + " (variant)";
                }
                return value;
            }

            return new UiUxDesignSpec
            {
                LayoutPattern = MutateField(spec.LayoutPattern),
                NavigationPattern = MutateField(spec.NavigationPattern),
                ColorPalette = MutateField(spec.ColorPalette),
                TypographyScale = MutateField(spec.TypographyScale),
                ComponentLibraryStyle = MutateField(spec.ComponentLibraryStyle),
                InteractionNotes = MutateField(spec.InteractionNotes),
                AccessibilityNotes = MutateField(spec.AccessibilityNotes),
            };
        }

        var parentSpec = _rng.NextDouble() < 0.5 ? a.Spec : b.Spec;
        var spec = Mutate(parentSpec);

        return new UiUxDesignCandidate
        {
            Id = $"gen{gen}-cand{index:000}",
            Name = $"{a.Name} x {b.Name} (child {index})",
            Summary = $"Child of {a.Id} and {b.Id}, mutated.",
            Spec = spec
        };
    }
}
