using AwesomeAssertions;
using LKCode.AddressManagement.Domain.Normalizers;

namespace LKCode.AddressManagement.UnitTests.Domain.Normalizers;

public class TextNormalizerTests
{
    [TestCase("Köln", "koln")]
    [TestCase("Koeln", "koln")]
    [TestCase("Koln", "koln")]
    [TestCase("KÖLN", "koln")]
    [TestCase("KOELN", "koln")]
    [TestCase("München", "munchen")]
    [TestCase("Muenchen", "munchen")]
    [TestCase("Düsseldorf", "dusseldorf")]
    [TestCase("Duesseldorf", "dusseldorf")]
    [TestCase("Lübeck", "lubeck")]
    [TestCase("Görlitz", "gorlitz")]
    [TestCase("Bärnau", "barnau")]
    [TestCase("Bad Säckingen", "bad sackingen")]
    [TestCase("Bad Saeckingen", "bad sackingen")]
    public void Normalize_GermanUmlauts_ReturnsSameValueAsDigraphsAndBaseLetters(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("Straße", "strasse")]
    [TestCase("STRASSE", "strasse")]
    [TestCase("Gießen", "giessen")]
    [TestCase("Giessen", "giessen")]
    [TestCase("Großenhain", "grossenhain")]
    [TestCase("GROẞENHAIN", "grossenhain")]
    public void Normalize_SharpS_ReturnsDoubleS(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("Ærøskøbing", "aroskobing")]
    [TestCase("Næstved", "nastved")]
    [TestCase("Sønderborg", "sonderborg")]
    [TestCase("Tromsø", "tromso")]
    [TestCase("Malmö", "malmo")]
    [TestCase("Århus", "arhus")]
    [TestCase("Jönköping", "jonkoping")]
    [TestCase("Þórshöfn", "thorshofn")]
    [TestCase("Ísafjörður", "isafjordur")]
    [TestCase("Reykjavík", "reykjavik")]
    public void Normalize_ScandinavianCharacters_ReturnsBaseLetters(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("Besançon", "besancon")]
    [TestCase("Orléans", "orleans")]
    [TestCase("Saint-Étienne", "saint-etienne")]
    [TestCase("Nîmes", "nimes")]
    [TestCase("Genève", "geneve")]
    [TestCase("Château-Thierry", "chateau-thierry")]
    [TestCase("Vandœuvre-lès-Nancy", "vandouvre-les-nancy")]
    public void Normalize_FrenchCharacters_ReturnsBaseLetters(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("Łódź", "lodz")]
    [TestCase("Kraków", "krakow")]
    [TestCase("Gdańsk", "gdansk")]
    [TestCase("Wrocław", "wroclaw")]
    [TestCase("Częstochowa", "czestochowa")]
    [TestCase("Białystok", "bialystok")]
    [TestCase("Bielsko-Biała", "bielsko-biala")]
    [TestCase("Żywiec", "zywiec")]
    public void Normalize_PolishCharacters_ReturnsBaseLetters(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("Đakovo", "dakovo")]
    [TestCase("Međimurje", "medimurje")]
    [TestCase("İstanbul", "istanbul")]
    [TestCase("Ilıca", "ilica")]
    [TestCase("Ħamrun", "hamrun")]
    [TestCase("São Paulo", "sao paulo")]
    [TestCase("A Coruña", "a coruna")]
    [TestCase("Málaga", "malaga")]
    [TestCase("Zürich", "zurich")]
    [TestCase("České Budějovice", "ceske budejovice")]
    [TestCase("Plzeň", "plzen")]
    public void Normalize_OtherLatinCharacters_ReturnsBaseLetters(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("ß", "ss")]
    [TestCase("ẞ", "ss")]
    [TestCase("æ", "a")]
    [TestCase("Æ", "a")]
    [TestCase("œ", "o")]
    [TestCase("Œ", "o")]
    [TestCase("ø", "o")]
    [TestCase("Ø", "o")]
    [TestCase("ł", "l")]
    [TestCase("Ł", "l")]
    [TestCase("đ", "d")]
    [TestCase("Đ", "d")]
    [TestCase("þ", "th")]
    [TestCase("Þ", "th")]
    [TestCase("ð", "d")]
    [TestCase("Ð", "d")]
    [TestCase("ı", "i")]
    [TestCase("İ", "i")]
    [TestCase("ħ", "h")]
    [TestCase("Ħ", "h")]
    [TestCase("ŧ", "t")]
    [TestCase("Ŧ", "t")]
    public void Normalize_SpecialCharacter_ReturnsReplacement(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("Berlin")]
    [TestCase("BERLIN")]
    [TestCase("berlin")]
    [TestCase("BeRlIn")]
    public void Normalize_AnyCase_ReturnsLowerCase(string value)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be("berlin");
    }

    [TestCase("  Frankfurt am Main  ")]
    [TestCase("Frankfurt   am   Main")]
    [TestCase("Frankfurt\tam\tMain")]
    [TestCase("Frankfurt\nam\r\nMain")]
    [TestCase("Frankfurt am Main")]
    [TestCase("\t Frankfurt \t am \n Main \r\n")]
    public void Normalize_Whitespace_TrimsAndCollapsesToSingleSpaces(string value)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be("frankfurt am main");
    }

    [TestCase("  BAD   SÄCKINGEN ", "bad sackingen")]
    [TestCase("  MÜNCHEN-Schwabing  ", "munchen-schwabing")]
    [TestCase("Hann.  Münden", "hann. munden")]
    [TestCase(" ŁÓDŹ  Bałuty ", "lodz baluty")]
    [TestCase("Saint-Étienne  CEDEX", "saint-etienne cedex")]
    public void Normalize_MixedForms_ReturnsNormalizedValue(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("Köln", "koln")]
    [TestCase("Orléans", "orleans")]
    [TestCase("Århus", "arhus")]
    public void Normalize_DecomposedCharacters_ReturnsSameValueAsComposedCharacters(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("Αθήνα", "αθηνα")]
    [TestCase("서울", "서울")]
    public void Normalize_NonLatinScript_RemovesAccentsAndKeepsLetters(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("Köln", "Koeln")]
    [TestCase("Köln", "KOLN")]
    [TestCase("Köln", "koln")]
    [TestCase("München", "MUENCHEN")]
    [TestCase("Göteborg", "Goeteborg")]
    [TestCase("Straße", "STRASSE")]
    [TestCase("Ærøskøbing", "Aeroeskoebing")]
    [TestCase("Łódź", "Lodz")]
    [TestCase("Saint-Étienne", "SAINT-ETIENNE")]
    [TestCase(" Bad  Säckingen ", "Bad Saeckingen")]
    public void Normalize_EquivalentSpellings_ReturnSameValue(string first, string second)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string firstResult = normalizer.Normalize(first);
        string secondResult = normalizer.Normalize(second);

        // Assert
        firstResult.Should().Be(secondResult);
    }

    [TestCase("Köln", "Kiel")]
    [TestCase("Berlin", "Bern")]
    [TestCase("Hamburg", "Homburg")]
    [TestCase("Mölln", "Müllheim")]
    [TestCase("Straße", "Strase")]
    [TestCase("Frankfurt am Main", "Frankfurt (Oder)")]
    public void Normalize_DifferentValues_ReturnDifferentValues(string first, string second)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string firstResult = normalizer.Normalize(first);
        string secondResult = normalizer.Normalize(second);

        // Assert
        firstResult.Should().NotBe(secondResult);
    }

    [TestCase("Frankfurt (Oder)", "frankfurt (oder)")]
    [TestCase("Frankfurt/Oder", "frankfurt/oder")]
    [TestCase("St. Gallen", "st. gallen")]
    [TestCase("L'Aquila", "l'aquila")]
    [TestCase("10115 Berlin", "10115 berlin")]
    [TestCase("Halle (Saale)", "halle (saale)")]
    public void Normalize_DigitsAndPunctuation_ArePreserved(string value, string expected)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().Be(expected);
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("   ")]
    [TestCase("\t\r\n ")]
    public void Normalize_EmptyOrWhitespace_ReturnsEmptyString(string value)
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        string result = normalizer.Normalize(value);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public void Normalize_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var normalizer = new TextNormalizer();

        // Act
        Action act = () => normalizer.Normalize(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("value");
    }
}
