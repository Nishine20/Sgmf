namespace Sgmf.Core;

public record Snapshot(decimal Km, decimal Hours, DateTime Date);

public record PlanStatus(int PlanId, string PlanName, PlanMetric Metric, decimal Threshold, decimal Used, decimal Percent, bool IsAlert);

public static class AlertRules
{
    public const decimal AlertFactor = 0.9m;

    public static bool IsAlert(decimal used, decimal threshold) => threshold > 0 && used >= AlertFactor * threshold;

    public static PlanStatus Evaluate(MaintenancePlan plan, Snapshot lastService, Snapshot current)
    {
        var used = plan.Metric switch
        {
            PlanMetric.Km => current.Km - lastService.Km,
            PlanMetric.Horas => current.Hours - lastService.Hours,
            _ => (decimal)(current.Date - lastService.Date).TotalDays
        };
        used = Math.Max(0, used);
        var percent = plan.Threshold > 0 ? Math.Round(used / plan.Threshold * 100, 1) : 0;
        return new PlanStatus(plan.Id, plan.Name, plan.Metric, plan.Threshold, used, percent, IsAlert(used, plan.Threshold));
    }
}

public record IndicatorInput(double OperatingHours, int Failures, double RepairHours, int Repairs);

public record IndicatorResult(double? MtbfHours, double? MttrHours, double? Availability, int Failures, int Repairs, double OperatingHours);

public static class IndicatorRules
{
    public static IndicatorInput Tally(DateTime from, DateTime to, IEnumerable<WorkOrder> workOrders)
    {
        var failures = workOrders
            .Where(w => w.Type == WorkOrderType.Correctiva && w.Status != WorkOrderStatus.Cancelada && w.OpenedAt >= from && w.OpenedAt <= to)
            .ToList();
        var closed = failures.Where(w => w.Status == WorkOrderStatus.Cerrada && w.ClosedAt != null).ToList();
        var downtime = failures.Sum(w => ((w.ClosedAt is { } c && c < to ? c : to) - w.OpenedAt).TotalHours);
        var repair = closed.Sum(w => (w.ClosedAt!.Value - w.OpenedAt).TotalHours);
        var total = Math.Max(0, (to - from).TotalHours);
        return new IndicatorInput(Math.Max(0, total - downtime), failures.Count, repair, closed.Count);
    }

    public static IndicatorResult Compute(IEnumerable<IndicatorInput> inputs)
    {
        var list = inputs.ToList();
        var operating = list.Sum(i => i.OperatingHours);
        var failures = list.Sum(i => i.Failures);
        var repairs = list.Sum(i => i.Repairs);
        var repairHours = list.Sum(i => i.RepairHours);
        double? mtbf = failures > 0 ? operating / failures : null;
        double? mttr = repairs > 0 ? repairHours / repairs : null;
        double? availability = failures == 0 ? 1.0
            : mtbf is not null && mttr is not null && mtbf + mttr > 0 ? mtbf / (mtbf + mttr) : null;
        return new IndicatorResult(mtbf, mttr, availability, failures, repairs, operating);
    }
}
