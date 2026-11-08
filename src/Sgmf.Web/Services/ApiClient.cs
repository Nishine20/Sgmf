using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Sgmf.Core;

namespace Sgmf.Web.Services;

public class ApiException(string message) : Exception(message);

public class ApiClient(HttpClient http, Session session)
{
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (session.Token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        if (body is not null) req.Content = JsonContent.Create(body, body.GetType());
        var resp = await http.SendAsync(req);
        if (resp.IsSuccessStatusCode) return resp;
        var message = resp.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Credenciales o sesión no válidas.",
            HttpStatusCode.Forbidden => "No tiene permiso para esta acción.",
            _ => await ReadDetail(resp)
        };
        throw new ApiException(message);
    }

    private static async Task<string> ReadDetail(HttpResponseMessage resp)
    {
        try
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("detail").GetString() ?? "Error en la solicitud.";
        }
        catch { return "Error en la solicitud."; }
    }

    private async Task<T> GetAsync<T>(string url) =>
        (await (await SendAsync(HttpMethod.Get, url)).Content.ReadFromJsonAsync<T>())!;

    private Task PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body ?? new { });

    public async Task<LoginResponse> LoginAsync(string user, string password) =>
        (await (await SendAsync(HttpMethod.Post, "api/auth/login", new LoginRequest(user, password))).Content.ReadFromJsonAsync<LoginResponse>())!;

    public Task<List<AssetDto>> Assets() => GetAsync<List<AssetDto>>("api/assets");
    public Task<AssetDetailDto> Asset(int id) => GetAsync<AssetDetailDto>($"api/assets/{id}");
    public Task CreateAsset(AssetInput i) => PostAsync("api/assets", i);
    public Task AddPlan(int assetId, PlanInput i) => PostAsync($"api/assets/{assetId}/plans", i);
    public Task AddReading(int assetId, ReadingInput i) => PostAsync($"api/assets/{assetId}/readings", i);

    public Task<List<WorkOrderDto>> WorkOrders() => GetAsync<List<WorkOrderDto>>("api/workorders");
    public Task OpenWorkOrder(WorkOrderInput i) => PostAsync("api/workorders", i);
    public Task StartWorkOrder(int id) => PostAsync($"api/workorders/{id}/start");
    public Task CloseWorkOrder(int id) => PostAsync($"api/workorders/{id}/close", new CloseWorkOrderInput(null, null));
    public Task CancelWorkOrder(int id) => PostAsync($"api/workorders/{id}/cancel");

    public Task<List<AlertDto>> Alerts() => GetAsync<List<AlertDto>>("api/alerts");
    public Task<DashboardDto> Dashboard() => GetAsync<DashboardDto>("api/dashboard");
}
