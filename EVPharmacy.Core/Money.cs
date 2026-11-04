using System.Globalization;

namespace EVPharmacy.Core;

/// <summary>Conversions between pesos (for people) and integer centavos (for storage).</summary>
public static class Money
{
    private static readonly CultureInfo Peso = new("en-PH");

    public static long ToCentavos(decimal pesos) =>
        (long)Math.Round(pesos * 100m, 0, MidpointRounding.AwayFromZero);

    public static decimal ToPesos(long centavos) => centavos / 100m;

    public static string Format(long centavos) =>
        ToPesos(centavos).ToString("C2", Peso);

    /// <summary>Parses user input such as "12.5", "1,250.00" or "₱12.50".</summary>
    public static bool TryParsePesos(string? text, out decimal pesos)
    {
        pesos = 0;
        return !string.IsNullOrWhiteSpace(text) &&
               decimal.TryParse(
                   text.Trim(),
                   NumberStyles.Currency,
                   Peso,
                   out pesos);
    }
}
