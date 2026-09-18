namespace LivingWorld.Definitions;
public sealed record NameDefinition
{
    public string Vowels { get; init; } = "";
    public string Consonants { get; init; } = "";
    public string[] Patterns { get; init; } = [];
    public int FirstMinSyllables { get; init; } = 2;
    public int FirstMaxSyllables { get; init; } = 3;
    public int LastMinSyllables { get; init; } = 2;
    public int LastMaxSyllables { get; init; } = 4;

    public void Validate()
    {
        if (Vowels.Length < 3 || Consonants.Length < 3 ||
            Vowels.Intersect(Consonants).Any() ||
            (Vowels + Consonants).Any(c => !char.IsLetter(c) || c is 'ё' or 'Ё') ||
            Vowels.Distinct().Count() != Vowels.Length || Consonants.Distinct().Count() != Consonants.Length)
            throw new InvalidDataException("Invalid name alphabet.");
        if (Patterns.Length == 0 || Patterns.Any(p => p.Length is < 1 or > 4 || !p.Contains('V') || p.Any(c => c is not ('V' or 'C'))))
            throw new InvalidDataException("Name patterns must contain V and use only V/C.");
        if (FirstMinSyllables < 1 || FirstMaxSyllables > 6 || FirstMaxSyllables < FirstMinSyllables ||
            LastMinSyllables < 1 || LastMaxSyllables > 6 || LastMaxSyllables < LastMinSyllables)
            throw new InvalidDataException("Invalid name length limits.");
    }
}
