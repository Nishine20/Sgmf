using System.Text.Json.Serialization;

namespace Sgmf.Core;

public static class Roles
{
    public const string Admin = "Administrador";
    public const string Maintenance = "JefeMantenimiento";
    public const string Sales = "Comercial";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssetType { Vehiculo, Maquinaria }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssetStatus { Disponible, EnMantenimiento, FueraDeServicio }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlanMetric { Km, Horas, Dias }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkOrderType { Preventiva, Correctiva }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkOrderStatus { Abierta, EnProceso, Cerrada, Cancelada }
