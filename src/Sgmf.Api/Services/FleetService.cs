using Microsoft.EntityFrameworkCore;
using Sgmf.Api.Data;
using Sgmf.Core;

namespace Sgmf.Api.Services;

public class FleetService(SgmfDb db)
{
    private static DateTime Now => DateTime.UtcNow;

    private IQueryable<Asset> Full => db.Assets.Include(a => a.Plans).Include(a => a.Readings).Include(a => a.WorkOrders);

    private static AssetDto ToDto(Asset a) =>
        new(a.Id, a.Code, a.Name, a.Type, a.Status, a.Brand, a.Model, a.Year, a.InServiceSince, a.CurrentKm, a.CurrentHours);

    private static WorkOrderDto ToDto(WorkOrder w, string code) =>
        new(w.Id, w.AssetId, code, w.PlanId, w.Type, w.Status, w.Description, w.OpenedAt, w.StartedAt, w.ClosedAt, w.KmAtService, w.HoursAtService);

    private async Task<Asset> GetAsset(int id) =>
        await Full.FirstOrDefaultAsync(a => a.Id == id) ?? throw new KeyNotFoundException("Activo no encontrado.");

    public async Task<List<AssetDto>> ListAssets() =>
        (await Full.OrderBy(a => a.Code).ToListAsync()).Select(ToDto).ToList();

    public async Task<AssetDetailDto> GetDetail(int id)
    {
        var a = await GetAsset(id);
        return new AssetDetailDto(ToDto(a), a.EvaluatePlans(Now),
            a.Readings.OrderByDescending(r => r.ReadingDate).Select(r => new ReadingDto(r.Id, r.ReadingDate, r.Km, r.Hours)).ToList(),
            a.WorkOrders.OrderByDescending(w => w.OpenedAt).Select(w => ToDto(w, a.Code)).ToList());
    }

    public async Task<AssetDto> CreateAsset(AssetInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Code) || string.IsNullOrWhiteSpace(i.Name))
            throw new InvalidOperationException("Código y nombre son obligatorios.");
        if (await db.Assets.AnyAsync(a => a.Code == i.Code))
            throw new InvalidOperationException("Ya existe un activo con ese código.");
        var a = new Asset
        {
            Code = i.Code.Trim(), Name = i.Name.Trim(), Type = i.Type, Brand = i.Brand, Model = i.Model,
            Year = i.Year, InServiceSince = i.InServiceSince
        };
        db.Assets.Add(a);
        await db.SaveChangesAsync();
        return ToDto(a);
    }

    public async Task AddPlan(int assetId, PlanInput i)
    {
        var a = await GetAsset(assetId);
        a.AddPlan(i.Name, i.Metric, i.Threshold);
        await db.SaveChangesAsync();
    }

    public async Task AddReading(int assetId, ReadingInput i)
    {
        var a = await GetAsset(assetId);
        a.AddReading(i.ReadingDate, i.Km, i.Hours);
        await db.SaveChangesAsync();
    }

    public async Task<List<WorkOrderDto>> ListWorkOrders(int? assetId)
    {
        var q = db.WorkOrders.AsQueryable();
        if (assetId is not null) q = q.Where(w => w.AssetId == assetId);
        var codes = await db.Assets.ToDictionaryAsync(a => a.Id, a => a.Code);
        return (await q.OrderByDescending(w => w.OpenedAt).ToListAsync()).Select(w => ToDto(w, codes[w.AssetId])).ToList();
    }

    public async Task<WorkOrderDto> OpenWorkOrder(WorkOrderInput i)
    {
        var a = await GetAsset(i.AssetId);
        var wo = a.OpenWorkOrder(i.Type, i.PlanId, i.Description, Now);
        await db.SaveChangesAsync();
        return ToDto(wo, a.Code);
    }

    public Task StartWorkOrder(int id) => Transition(id, (a, w) => w.Start(Now));

    public Task CancelWorkOrder(int id) => Transition(id, (a, w) => w.Cancel(Now));

    public Task CloseWorkOrder(int id, CloseWorkOrderInput i) => Transition(id, (a, w) => a.CloseWorkOrder(w, Now, i.Km, i.Hours));

    private async Task Transition(int id, Action<Asset, WorkOrder> action)
    {
        var assetId = await db.WorkOrders.Where(w => w.Id == id).Select(w => (int?)w.AssetId).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Orden no encontrada.");
        var a = await GetAsset(assetId);
        action(a, a.WorkOrders.First(w => w.Id == id));
        a.RefreshStatus();
        await db.SaveChangesAsync();
    }

    public async Task<List<AlertDto>> GetAlerts()
    {
        var now = Now;
        return (await Full.ToListAsync())
            .SelectMany(a => a.EvaluatePlans(now).Where(p => p.IsAlert).Select(p => new AlertDto(a.Id, a.Code, a.Name, p)))
            .OrderByDescending(x => x.Plan.Percent).ToList();
    }

    public async Task<DashboardDto> GetDashboard()
    {
        var now = Now;
        var assets = await Full.OrderBy(a => a.Code).ToListAsync();
        var tallies = assets.Select(a => IndicatorRules.Tally(a.InServiceSince, now, a.WorkOrders)).ToList();
        var rows = assets.Select((a, n) => new AssetAvailabilityDto(a.Id, a.Code, a.Name, a.Status, IndicatorRules.Compute([tallies[n]]))).ToList();
        return new DashboardDto(
            assets.Count,
            assets.Count(a => a.Status == AssetStatus.Disponible),
            assets.Count(a => a.Status == AssetStatus.EnMantenimiento),
            assets.Count(a => a.Status == AssetStatus.FueraDeServicio),
            assets.Sum(a => a.EvaluatePlans(now).Count(p => p.IsAlert)),
            IndicatorRules.Compute(tallies), rows);
    }
}
