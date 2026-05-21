using Blazored.SessionStorage;
using Kolekta.Web.Components;
using Kolekta.Web.Data;
using Kolekta.Web.Models.User;
using Kolekta.Web.Services.Drops;
using Kolekta.Web.Services.Inventory;
using Kolekta.Web.Services.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// ADD THIS
builder.WebHost.UseUrls("http://0.0.0.0:10000");
#region DATABASE
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);
#endregion

#region SERVICES
builder.Services.AddScoped<DemoUserService>();
builder.Services.AddScoped<DemoInventoryService>();

builder.Services.AddScoped<DropService>();

builder.Services.AddSingleton<CharacterPoolBuilder>();
builder.Services.AddSingleton<CharacterSeeder>();

builder.Services.AddSingleton<HttpClient>();
builder.Services.AddBlazoredSessionStorage();
#endregion

#region IDENTITY
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();
#endregion

#region BLAZOR
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
#endregion

var app = builder.Build();

#region BACKGROUND CHARACTER SEED (NON-BLOCKING)
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();

        var seeder =
            scope.ServiceProvider.GetRequiredService<CharacterSeeder>();

        await seeder.SeedAsync();
    }
    catch
    {
        // log later if needed
    }
});
#endregion

#region PIPELINE
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
#endregion

app.Run();