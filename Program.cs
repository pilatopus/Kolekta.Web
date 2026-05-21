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

#region DATABASE
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);
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

#region SERVICES
builder.Services.AddScoped<DemoUserService>();
builder.Services.AddScoped<DemoInventoryService>();
builder.Services.AddScoped<DropService>();

builder.Services.AddSingleton<CharacterPoolBuilder>();
builder.Services.AddSingleton<CharacterSeeder>();

builder.Services.AddBlazoredSessionStorage();
builder.Services.AddHttpClient();
#endregion

#region BLAZOR (IMPORTANT FIX HERE)
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
#endregion

var app = builder.Build();

#region BACKGROUND SEED (SAFE)
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<CharacterSeeder>();
        await seeder.SeedAsync();
    }
    catch
    {
        // ignore startup failure
    }
});
#endregion

#region PIPELINE (CRITICAL FIXES)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles(); // 🔥 REQUIRED FOR _framework FILES

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// 🔥 THIS IS WHAT FIXES BLazor 404 ISSUES
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
#endregion