using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sgmf.Core;

namespace Sgmf.Api.Data;

public static class Seeder
{
    public static async Task SeedAsync(SgmfDb db, string password)
    {
        var hasher = new PasswordHasher<AppUser>();
        foreach (var (user, role) in new[] { ("admin", Roles.Admin), ("jefe", Roles.Maintenance), ("comercial", Roles.Sales) })
        {
            if (await db.Users.AnyAsync(u => u.UserName == user)) continue;
            var u = new AppUser { UserName = user, Role = role };
            u.PasswordHash = hasher.HashPassword(u, password);
            db.Users.Add(u);
        }

        var existing = await db.Assets.Select(a => a.Code).ToListAsync();
        for (var i = 0; i < 10; i++)
        {
            var code = $"ACT-{i + 1:000}";
            if (existing.Contains(code)) continue;
            var asset = Build(i, code);
            db.Assets.Add(asset);
            await db.SaveChangesAsync();   // genera los Id de los planes
            AddHistory(asset, i);
            asset.RefreshStatus();
            await db.SaveChangesAsync();
        }
        await db.SaveChangesAsync();
    }

    private static Asset Build(int i, string code)
    {
        var vehicle = i < 5;
        var idx = i % 5;
        var asset = new Asset
        {
            Code = code,
            Type = vehicle ? AssetType.Vehiculo : AssetType.Maquinaria,
            Name = vehicle ? $"Camioneta {idx + 1}" : $"Excavadora {idx + 1}",
            Brand = vehicle ? "Toyota" : "Caterpillar",
            Model = vehicle ? "Hilux" : "320",
            Year = 2022 + idx % 3,
            InServiceSince = new DateTime(2026, 1, 1).AddDays(10 * i)
        };

        var km = vehicle ? 7500m + idx * 600 : 0m;
        var hours = vehicle ? km / 40 : 150m + idx * 30;
        for (var q = 1; q <= 4; q++)
            asset.Readings.Add(new UsageReading
            {
                ReadingDate = new DateTime(2026, 2 * q + 2, 1),
                Km = km * q / 4,
                Hours = hours * q / 4
            });

        asset.AddPlan("Cambio de aceite", vehicle ? PlanMetric.Km : PlanMetric.Horas, vehicle ? 10000 : 250);
        asset.AddPlan(vehicle ? "Revisión anual" : "Revisión hidráulica", vehicle ? PlanMetric.Dias : PlanMetric.Horas, vehicle ? 365 : 500);
        return asset;
    }

    private static void AddHistory(Asset asset, int i)
    {
        var idx = i % 5;
        var oil = asset.Plans[0];

        // Preventiva cerrada a mitad de uso para los dos primeros de cada grupo (sin alerta de aceite).
        if (idx < 2)
        {
            var wo = Closed(asset, WorkOrderType.Preventiva, oil.Id, "Cambio de aceite", new DateTime(2026, 6, 15, 8, 0, 0), 3);
            wo.KmAtService = asset.CurrentKm / 2;
            wo.HoursAtService = asset.CurrentHours / 2;
        }
        if (i % 3 == 0)
            Closed(asset, WorkOrderType.Correctiva, null, "Falla de frenos o hidráulica", new DateTime(2026, 5, 10, 8, 0, 0).AddDays(i), 4 + i);
        if (i % 2 == 1)
            Closed(asset, WorkOrderType.Correctiva, null, "Falla eléctrica", new DateTime(2026, 8, 3, 9, 0, 0).AddDays(i), 6);

        if (i == 7)
            asset.OpenWorkOrder(WorkOrderType.Correctiva, null, "Falla de motor", new DateTime(2026, 11, 5, 9, 0, 0));
        if (i == 2)
            asset.OpenWorkOrder(WorkOrderType.Preventiva, oil.Id, "Cambio de aceite programado", new DateTime(2026, 11, 6, 9, 0, 0))
                .Start(new DateTime(2026, 11, 6, 10, 0, 0));
    }

    private static WorkOrder Closed(Asset asset, WorkOrderType type, int? planId, string description, DateTime opened, int repairHours)
    {
        var wo = asset.OpenWorkOrder(type, planId, description, opened);
        wo.Status = WorkOrderStatus.Cerrada;
        wo.StartedAt = opened;
        wo.ClosedAt = opened.AddHours(repairHours);
        return wo;
    }
}
