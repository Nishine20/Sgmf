namespace Sgmf.Core;

public class AppUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
}

public class MaintenancePlan
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string Name { get; set; } = "";
    public PlanMetric Metric { get; set; }
    public decimal Threshold { get; set; }
}

public class UsageReading
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public DateTime ReadingDate { get; set; }
    public decimal Km { get; set; }
    public decimal Hours { get; set; }
}

public class WorkOrder
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public int? PlanId { get; set; }
    public WorkOrderType Type { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Abierta;
    public string Description { get; set; } = "";
    public DateTime OpenedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? KmAtService { get; set; }
    public decimal? HoursAtService { get; set; }

    public bool IsActive => Status is WorkOrderStatus.Abierta or WorkOrderStatus.EnProceso;

    public void Start(DateTime now)
    {
        if (Status != WorkOrderStatus.Abierta)
            throw new InvalidOperationException("Solo se puede iniciar una orden abierta.");
        Status = WorkOrderStatus.EnProceso;
        StartedAt = now;
    }

    public void Cancel(DateTime now)
    {
        if (!IsActive)
            throw new InvalidOperationException("Solo se puede cancelar una orden abierta o en proceso.");
        Status = WorkOrderStatus.Cancelada;
        ClosedAt = now;
    }

    internal void Close(DateTime now, decimal km, decimal hours)
    {
        if (Status != WorkOrderStatus.EnProceso)
            throw new InvalidOperationException("Solo se puede cerrar una orden en proceso.");
        Status = WorkOrderStatus.Cerrada;
        ClosedAt = now;
        KmAtService = km;
        HoursAtService = hours;
    }
}

public class Asset
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AssetType Type { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Disponible;
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public int Year { get; set; }
    public DateTime InServiceSince { get; set; }
    public List<MaintenancePlan> Plans { get; set; } = [];
    public List<UsageReading> Readings { get; set; } = [];
    public List<WorkOrder> WorkOrders { get; set; } = [];

    public decimal CurrentKm => Readings.Count == 0 ? 0 : Readings.Max(r => r.Km);
    public decimal CurrentHours => Readings.Count == 0 ? 0 : Readings.Max(r => r.Hours);

    public UsageReading AddReading(DateTime date, decimal km, decimal hours)
    {
        if (km < 0 || hours < 0)
            throw new InvalidOperationException("Las lecturas no pueden ser negativas.");
        if (km < CurrentKm || hours < CurrentHours)
            throw new InvalidOperationException("La lectura no puede ser menor que la última registrada.");
        var reading = new UsageReading { AssetId = Id, ReadingDate = date, Km = km, Hours = hours };
        Readings.Add(reading);
        return reading;
    }

    public MaintenancePlan AddPlan(string name, PlanMetric metric, decimal threshold)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("El nombre del plan es obligatorio.");
        if (threshold <= 0) throw new InvalidOperationException("El umbral debe ser mayor que cero.");
        var plan = new MaintenancePlan { AssetId = Id, Name = name.Trim(), Metric = metric, Threshold = threshold };
        Plans.Add(plan);
        return plan;
    }

    public WorkOrder OpenWorkOrder(WorkOrderType type, int? planId, string description, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new InvalidOperationException("La descripción es obligatoria.");
        if (type == WorkOrderType.Preventiva && planId is not null && Plans.All(p => p.Id != planId))
            throw new InvalidOperationException("El plan no pertenece al activo.");
        var wo = new WorkOrder
        {
            AssetId = Id,
            PlanId = type == WorkOrderType.Preventiva ? planId : null,
            Type = type,
            Description = description.Trim(),
            OpenedAt = now
        };
        WorkOrders.Add(wo);
        RefreshStatus();
        return wo;
    }

    public void CloseWorkOrder(WorkOrder wo, DateTime now, decimal? km, decimal? hours)
    {
        var closeKm = km ?? CurrentKm;
        var closeHours = hours ?? CurrentHours;
        if (closeKm < CurrentKm || closeHours < CurrentHours)
            throw new InvalidOperationException("Las lecturas al cierre no pueden ser menores que la última registrada.");
        if (wo.Status != WorkOrderStatus.EnProceso)
            throw new InvalidOperationException("Solo se puede cerrar una orden en proceso.");
        if (closeKm > CurrentKm || closeHours > CurrentHours) AddReading(now, closeKm, closeHours);
        wo.Close(now, closeKm, closeHours);
        RefreshStatus();
    }

    public void RefreshStatus()
    {
        var active = WorkOrders.Where(w => w.IsActive).ToList();
        Status = active.Any(w => w.Type == WorkOrderType.Correctiva) ? AssetStatus.FueraDeServicio
               : active.Count > 0 ? AssetStatus.EnMantenimiento
               : AssetStatus.Disponible;
    }

    public List<PlanStatus> EvaluatePlans(DateTime now)
    {
        var current = new Snapshot(CurrentKm, CurrentHours, now);
        return Plans.Select(p =>
        {
            var last = WorkOrders
                .Where(w => w.Type == WorkOrderType.Preventiva && w.Status == WorkOrderStatus.Cerrada && w.PlanId == p.Id && w.ClosedAt != null)
                .OrderByDescending(w => w.ClosedAt).FirstOrDefault();
            var baseline = last is null
                ? new Snapshot(0, 0, InServiceSince)
                : new Snapshot(last.KmAtService ?? 0, last.HoursAtService ?? 0, last.ClosedAt!.Value);
            return AlertRules.Evaluate(p, baseline, current);
        }).ToList();
    }
}
