namespace Sgmf.Core;

public record LoginRequest(string UserName, string Password);
public record LoginResponse(string Token, string UserName, string Role);

public record AssetInput(string Code, string Name, AssetType Type, string Brand, string Model, int Year, DateTime InServiceSince);
public record AssetDto(int Id, string Code, string Name, AssetType Type, AssetStatus Status, string Brand, string Model, int Year, DateTime InServiceSince, decimal CurrentKm, decimal CurrentHours);

public record PlanInput(string Name, PlanMetric Metric, decimal Threshold);
public record ReadingInput(DateTime ReadingDate, decimal Km, decimal Hours);
public record ReadingDto(int Id, DateTime ReadingDate, decimal Km, decimal Hours);

public record WorkOrderInput(int AssetId, int? PlanId, WorkOrderType Type, string Description);
public record CloseWorkOrderInput(decimal? Km, decimal? Hours);
public record WorkOrderDto(int Id, int AssetId, string AssetCode, int? PlanId, WorkOrderType Type, WorkOrderStatus Status, string Description, DateTime OpenedAt, DateTime? StartedAt, DateTime? ClosedAt, decimal? KmAtService, decimal? HoursAtService);

public record AssetDetailDto(AssetDto Asset, List<PlanStatus> Plans, List<ReadingDto> Readings, List<WorkOrderDto> WorkOrders);
public record AlertDto(int AssetId, string AssetCode, string AssetName, PlanStatus Plan);
public record AssetAvailabilityDto(int AssetId, string Code, string Name, AssetStatus Status, IndicatorResult Indicators);
public record DashboardDto(int Total, int Available, int InMaintenance, int OutOfService, int AlertCount, IndicatorResult Fleet, List<AssetAvailabilityDto> Assets);
