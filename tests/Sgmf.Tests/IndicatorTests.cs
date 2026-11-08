using Sgmf.Core;

namespace Sgmf.Tests;

public class IndicatorTests
{
    private static readonly DateTime From = new(2026, 1, 1);
    private static readonly DateTime To = From.AddHours(1000);

    private static WorkOrder Failure(double openedHour, double? repairHours) => new()
    {
        Type = WorkOrderType.Correctiva,
        OpenedAt = From.AddHours(openedHour),
        Status = repairHours is null ? WorkOrderStatus.Abierta : WorkOrderStatus.Cerrada,
        ClosedAt = repairHours is null ? null : From.AddHours(openedHour + repairHours.Value)
    };

    [Fact]
    public void Mtbf_mttr_and_availability_follow_the_formulas()
    {
        // 1000 h de periodo, 2 fallas de 10 h y 30 h => operativo 960 h.
        var input = IndicatorRules.Tally(From, To, [Failure(100, 10), Failure(500, 30)]);
        var r = IndicatorRules.Compute([input]);

        Assert.Equal(480, r.MtbfHours!.Value, 6);      // 960 / 2
        Assert.Equal(20, r.MttrHours!.Value, 6);       // 40 / 2
        Assert.Equal(480.0 / 500.0, r.Availability!.Value, 6);
        Assert.Equal(2, r.Failures);
    }

    [Fact]
    public void No_failures_means_full_availability_and_no_mtbf()
    {
        var r = IndicatorRules.Compute([IndicatorRules.Tally(From, To, [])]);
        Assert.Null(r.MtbfHours);
        Assert.Null(r.MttrHours);
        Assert.Equal(1.0, r.Availability);
    }

    [Fact]
    public void Preventive_and_cancelled_orders_are_not_failures()
    {
        var preventive = new WorkOrder { Type = WorkOrderType.Preventiva, OpenedAt = From.AddHours(10), Status = WorkOrderStatus.Cerrada, ClosedAt = From.AddHours(12) };
        var cancelled = new WorkOrder { Type = WorkOrderType.Correctiva, OpenedAt = From.AddHours(20), Status = WorkOrderStatus.Cancelada, ClosedAt = From.AddHours(21) };
        Assert.Equal(0, IndicatorRules.Tally(From, To, [preventive, cancelled]).Failures);
    }

    [Fact]
    public void Open_failure_counts_downtime_but_no_repair_yet()
    {
        var r = IndicatorRules.Compute([IndicatorRules.Tally(From, To, [Failure(900, null)])]);
        Assert.Equal(900, r.MtbfHours!.Value, 6);      // 1000 - 100 h caídas
        Assert.Null(r.MttrHours);
        Assert.Null(r.Availability);
    }

    [Fact]
    public void Fleet_aggregates_totals_across_assets()
    {
        var a = IndicatorRules.Tally(From, To, [Failure(100, 10)]);
        var b = IndicatorRules.Tally(From, To, [Failure(200, 30), Failure(600, 20)]);
        var fleet = IndicatorRules.Compute([a, b]);

        Assert.Equal(3, fleet.Failures);
        Assert.Equal((990 + 950) / 3.0, fleet.MtbfHours!.Value, 6);
        Assert.Equal(20, fleet.MttrHours!.Value, 6);
    }
}
