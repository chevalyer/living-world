namespace LivingWorld.Simulation;
public sealed class NameGenerator(NameDefinition names)
{
    public (string First, string Last) Generate(DeterministicRandom random, string sex)
    {
        return (GenerateWord(random, names.FirstMinSyllables, names.FirstMaxSyllables),
            GenerateWord(random, names.LastMinSyllables, names.LastMaxSyllables));
    }

    public string GenerateWord(DeterministicRandom random, int minimum, int maximum)
    {
        var letters=new System.Text.StringBuilder();
        var count=random.Range(minimum,maximum+1);
        var previousKind='\0';
        var run=0;
        for(var syllable=0;syllable<count;syllable++)
        {
            var pattern=names.Patterns[random.Range(0,names.Patterns.Length)];
            foreach(var requested in pattern)
            {
                var kind=requested==previousKind&&run==2?(requested=='V'?'C':'V'):requested;
                var alphabet=kind=='V'?names.Vowels:names.Consonants;
                var index=random.Range(0,alphabet.Length);
                if(letters.Length>0&&alphabet[index]==letters[letters.Length-1])index=(index+1)%alphabet.Length;
                letters.Append(alphabet[index]);
                run=kind==previousKind?run+1:1;
                previousKind=kind;
            }
        }
        letters[0]=char.ToUpperInvariant(letters[0]);
        return letters.ToString();
    }
}
