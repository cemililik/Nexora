using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Value object for phone numbers with country code.
/// </summary>
public sealed record PhoneNumber
{
    public string CountryCode { get; }
    public string Number { get; }

    public PhoneNumber(string countryCode, string number)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            throw new DomainException("lockey_shared_phone_country_code_required");
        if (string.IsNullOrWhiteSpace(number))
            throw new DomainException("lockey_shared_phone_number_required");

        CountryCode = countryCode.TrimStart('+');
        Number = number;
    }

    public override string ToString() => $"+{CountryCode}{Number}";
}
