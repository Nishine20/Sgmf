using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sgmf.Api;
using Sgmf.Api.Data;
using Sgmf.Api.Services;
using Sgmf.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SgmfDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<FleetService>();
builder.Services.AddScoped<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.FromMinutes(1)
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Endpoints.ManagePolicy, p => p.RequireRole(Roles.Admin, Roles.Maintenance));

var app = builder.Build();

app.UseExceptionHandler(h => h.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex switch
    {
        InvalidOperationException => (400, ex.Message),
        KeyNotFoundException => (404, ex.Message),
        _ => (500, "Error interno.")
    };
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { title, detail = title });
}));
app.UseAuthentication();
app.UseAuthorization();
app.MapSgmf();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SgmfDb>();
    await db.Database.MigrateAsync();
    await Seeder.SeedAsync(db, app.Configuration["Seed:Password"] ?? throw new InvalidOperationException("Falta Seed:Password."));
}

app.Run();

public partial class Program;
