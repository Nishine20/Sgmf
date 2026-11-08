using Sgmf.Core;

namespace Sgmf.Tests;

public class AlertTests
{
    private static readonly DateTime T0 = new(2026, 1, 1);

    [Theory]
    [InlineData(PlanMetric.Km, 10000, 8999, false)]
    [InlineData(PlanMetric.Km, 10000, 9000, true)]
    [InlineData(PlanMetric.Km, 10000, 12000, true)]
    [InlineData(PlanMetric.Horas, 250, 224, false)]
    [InlineData(PlanMetric.Horas, 250, 225, true)]
    public void Alert_triggers_at_90_percent_of_threshold(PlanMetric metric, decimal threshold, decimal used, bool expected)
    {
        var plan = new MaintenancePlan { Id = 1, Metric = metric, Threshold = threshold };
        var last = new Snapshot(1000, 100, T0);
        var current = new Snapshot(1000 + used, 100 + used, T0);

        var status = AlertRules.Evaluate(plan, last, current);

        Assert.Equal(expected, status.IsAlert);
        Assert.Equal(used, status.Used);
    }

    [Theory]
    [InlineData(161, false)]
    [InlineData(162, true)]
    public void Time_plan_uses_days_since_last_service(int days, bool expected)
    {
        var plan = new MaintenancePlan { Metric = PlanMetric.Dias, Threshold = 180 };
        var status = AlertRules.Evaluate(plan, new Snapshot(0, 0, T0), new Snapshot(0, 0, T0.AddDays(days)));
        Assert.Equal(expected, status.IsAlert);
    }

    [Fact]
    public void Zero_threshold_never_alerts()
    {
        var plan = new MaintenancePlan { Metric = PlanMetric.Km, Threshold = 0 };
        Assert.False(AlertRules.Evaluate(plan, new Snapshot(0, 0, T0), new Snapshot(5000, 0, T0)).IsAlert);
    }

    [Fact]
    public void Closing_a_preventive_order_resets_the_plan_baseline()
    {
        var asset = new Asset { InServiceSince = T0 };
        var plan = asset.AddPlan("Aceite", PlanMetric.Km, 10000);
        plan.Id = 1;
        asset.AddReading(T0.AddDays(60), 9500, 0);
        Assert.True(asset.EvaluatePlans(T0.AddDays(61)).Single().IsAlert);

        var wo = asset.OpenWorkOrder(WorkOrderType.Preventiva, 1, "Aceite", T0.AddDays(62));
        wo.Start(T0.AddDays(62));
        asset.CloseWorkOrder(wo, T0.AddDays(63), null, null);
        asset.AddReading(T0.AddDays(90), 10500, 0);

        var status = asset.EvaluatePlans(T0.AddDays(91)).Single();
        Assert.Equal(1000, status.Used);
        Assert.False(status.IsAlert);
    }

    [Fact]
    public void Readings_cannot_decrease()
    {
        var asset = new Asset();
        asset.AddReading(T0, 100, 10);
        Assert.Throws<InvalidOperationException>(() => asset.AddReading(T0.AddDays(1), 90, 10));
    }

    [Fact]
    public void Work_order_lifecycle_updates_asset_status()
    {
        var asset = new Asset();
        var wo = asset.OpenWorkOrder(WorkOrderType.Correctiva, null, "Falla", T0);
        Assert.Equal(AssetStatus.FueraDeServicio, asset.Status);

        Assert.Throws<InvalidOperationException>(() => asset.CloseWorkOrder(wo, T0, null, null));
        wo.Start(T0);
        asset.CloseWorkOrder(wo, T0.AddHours(5), null, null);

        Assert.Equal(WorkOrderStatus.Cerrada, wo.Status);
        Assert.Equal(AssetStatus.Disponible, asset.Status);
    }
}
