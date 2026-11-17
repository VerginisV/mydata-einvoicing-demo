using MyData.Client.Model;

namespace MyData.Client;

/// <summary>
/// Catches common mistakes before sending, so they fail fast with a clear message
/// instead of a myDATA validation error.
/// </summary>
public static class InvoiceValidator
{
    public static IReadOnlyList<string> Validate(Invoice invoice, DateOnly today)
    {
        var errors = new List<string>();

        CheckParty(invoice.Issuer, "Issuer", errors);
        CheckParty(invoice.Counterpart, "Counterpart", errors);

        if (invoice.Issuer.Country == "GR" && invoice.Counterpart.Country == "GR"
            && invoice.Issuer.VatNumber == invoice.Counterpart.VatNumber)
            errors.Add("Issuer and counterpart must be different businesses.");

        if (string.IsNullOrWhiteSpace(invoice.Series))
            errors.Add("Series is required (use \"0\" if you have no series).");
        if (string.IsNullOrWhiteSpace(invoice.Number))
            errors.Add("Invoice number (aa) is required.");
        if (invoice.IssueDate > today)
            errors.Add("Issue date cannot be in the future.");
        if (invoice.Currency.Length != 3)
            errors.Add("Currency must be a 3-letter ISO 4217 code.");
        if (invoice.Lines.Count == 0)
            errors.Add("An invoice needs at least one line.");

        for (var i = 0; i < invoice.Lines.Count; i++)
        {
            var line = invoice.Lines[i];
            var n = i + 1;
            if (line.NetValue <= 0)
                errors.Add($"Line {n}: net value must be positive.");
            if (decimal.Round(line.NetValue, 2) != line.NetValue)
                errors.Add($"Line {n}: net value can have at most 2 decimals.");
            if (!Enum.IsDefined(line.VatCategory))
                errors.Add($"Line {n}: unknown VAT category.");
            if (line.VatCategory == VatCategory.Vat0 && line.VatExemptionCategory is null)
                errors.Add($"Line {n}: 0% VAT needs a VAT exemption category.");
            if (string.IsNullOrWhiteSpace(line.Classification.Type) || string.IsNullOrWhiteSpace(line.Classification.Category))
                errors.Add($"Line {n}: income classification type and category are required.");
        }

        return errors;
    }

    /// <summary>
    /// Checks the Greek VAT number (ΑΦΜ) check digit: each of the first 8 digits is multiplied
    /// by 2^(8-position), and the sum mod 11 mod 10 must equal the 9th digit.
    /// </summary>
    public static bool IsValidGreekVatNumber(string? vatNumber)
    {
        if (vatNumber is null || vatNumber.Length != 9 || !vatNumber.All(char.IsAsciiDigit) || vatNumber == "000000000")
            return false;

        var sum = 0;
        for (var i = 0; i < 8; i++)
            sum += (vatNumber[i] - '0') << (8 - i);

        return sum % 11 % 10 == vatNumber[8] - '0';
    }

    private static void CheckParty(Party party, string role, List<string> errors)
    {
        if (party.Country.Length != 2)
            errors.Add($"{role}: country must be a 2-letter ISO code.");
        if (party.Country == "GR" && !IsValidGreekVatNumber(party.VatNumber))
            errors.Add($"{role}: '{party.VatNumber}' is not a valid Greek VAT number (ΑΦΜ).");
        if (party.Branch < 0)
            errors.Add($"{role}: branch cannot be negative.");
    }
}
