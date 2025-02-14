using Microsoft.AspNetCore.Identity;

namespace FreshFarmMarket;

public class FreshFarmMarketUser : IdentityUser
{
    [PersonalData]
    public string FullName { get; set; } = string.Empty;

    [PersonalData]
    public string CreditCardNumber { get; set; } = string.Empty;

    [PersonalData]
    public string Gender { get; set; } = string.Empty;

    [PersonalData]
    public string MobileNumber { get; set; } = string.Empty;

    [PersonalData]
    public string DeliveryAddress { get; set; } = string.Empty;

    [PersonalData]
    public string PhotoUrl { get; set; } = string.Empty;

    [PersonalData]
    public string AboutMe { get; set; } = string.Empty;
}
