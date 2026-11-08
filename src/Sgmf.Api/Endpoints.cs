using Microsoft.AspNetCore.Mvc;
using Sgmf.Api.Services;
using Sgmf.Core;

namespace Sgmf.Api;

public static class Endpoints
{
    public const string ManagePolicy = "Manage";

    public static void MapSgmf(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest r, TokenService t) =>
            await t.LoginAsync(r) is { } res ? Results.Ok(res) : Results.Unauthorized()).AllowAnonymous();

        var api = app.MapGroup("/api").RequireAuthorization();
        var manage = api.MapGroup("").RequireAuthorization(ManagePolicy);

        api.MapGet("/assets", (FleetService s) => s.ListAssets());
        api.MapGet("/assets/{id:int}", (int id, FleetService s) => s.GetDetail(id));
        api.MapGet("/workorders", (int? assetId, FleetService s) => s.ListWorkOrders(assetId));
        api.MapGet("/alerts", (FleetService s) => s.GetAlerts());
        api.MapGet("/dashboard", (FleetService s) => s.GetDashboard());

        api.MapPost("/assets", async (AssetInput i, FleetService s) => Results.Ok(await s.CreateAsset(i)))
            .RequireAuthorization(p => p.RequireRole(Roles.Admin));

        manage.MapPost("/assets/{id:int}/plans", async (int id, PlanInput i, FleetService s) => { await s.AddPlan(id, i); return Results.NoContent(); });
        manage.MapPost("/assets/{id:int}/readings", async (int id, ReadingInput i, FleetService s) => { await s.AddReading(id, i); return Results.NoContent(); });
        manage.MapPost("/workorders", async (WorkOrderInput i, FleetService s) => Results.Ok(await s.OpenWorkOrder(i)));
        manage.MapPost("/workorders/{id:int}/start", async (int id, FleetService s) => { await s.StartWorkOrder(id); return Results.NoContent(); });
        manage.MapPost("/workorders/{id:int}/close", async (int id, CloseWorkOrderInput i, FleetService s) => { await s.CloseWorkOrder(id, i); return Results.NoContent(); });
        manage.MapPost("/workorders/{id:int}/cancel", async (int id, FleetService s) => { await s.CancelWorkOrder(id); return Results.NoContent(); });
    }
}
