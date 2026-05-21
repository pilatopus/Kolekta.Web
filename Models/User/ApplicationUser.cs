using Microsoft.AspNetCore.Identity;

namespace Kolekta.Web.Models.User;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";

    public int Coins { get; set; } = 0;

    public string AvatarUrl { get; set; } = "";
}