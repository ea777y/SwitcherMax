using SwitcherMax;

var cases = new (string Name, string Source, ConversionMode Mode, bool Smart, string? Expected)[]
{
    ("EN to RU lowercase", "ghbdtn", ConversionMode.Auto, true, "привет"),
    ("EN to RU uppercase", "GHBDTN", ConversionMode.Auto, true, "ПРИВЕТ"),
    ("RU to EN", "руддщ", ConversionMode.Auto, true, "hello"),
    ("Smart punctuation EN", "Ghbdtn, vbh!", ConversionMode.Auto, true, "Привет, мир!"),
    ("Smart punctuation RU", "руддщ, цщкдв!", ConversionMode.Auto, true, "hello, world!"),
    ("Keyboard layout EN", "qwerty", ConversionMode.EnglishToRussian, true, "йцукен"),
    ("Keyboard layout RU", "йцукен", ConversionMode.RussianToEnglish, true, "qwerty"),
    ("Mixed capitalization", "AbC", ConversionMode.EnglishToRussian, true, "ФиС"),
    ("No letters", "1234", ConversionMode.Auto, true, null),
    ("Tie does not modify", "abcабв", ConversionMode.Auto, true, null),
    ("Multiline", "ghbdtn\nvbh", ConversionMode.Auto, true, "привет\nмир"),
    ("Numbers intact", "ghbdtn 2026", ConversionMode.Auto, true, "привет 2026"),
    ("Strict EN comma key", "a,b", ConversionMode.EnglishToRussian, false, "фби"),
    ("Preserve normal comma", "a, b", ConversionMode.EnglishToRussian, true, "ф, и"),
    ("Period at sentence end", "ghbdtn.", ConversionMode.Auto, true, "привет."),
};
int failed = 0;
foreach (var (name, source, mode, smart, expected) in cases)
{
    string? actual = Conversion.Convert(source, mode, smart);
    bool pass = actual == expected;
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")} | {name} | {actual ?? "<unchanged>"}");
    if (!pass) failed++;
}
Console.WriteLine($"CHECKS: {cases.Length}; PASSED: {cases.Length - failed}; FAILED: {failed}");
return failed == 0 ? 0 : 1;
