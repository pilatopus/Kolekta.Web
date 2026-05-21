namespace Kolekta.Web.Services.User;

public class DemoUserService
{
    public string UserId { get; } = Guid.NewGuid().ToString();
}