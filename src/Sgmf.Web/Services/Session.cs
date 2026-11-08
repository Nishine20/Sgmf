using Sgmf.Core;

namespace Sgmf.Web.Services;

public class Session
{
    public string? Token { get; private set; }
    public string? UserName { get; private set; }
    public string? Role { get; private set; }

    public bool IsLoggedIn => Token is not null;
    public bool IsAdmin => Role == Roles.Admin;
    public bool CanManage => Role is Roles.Admin or Roles.Maintenance;

    public void Set(LoginResponse r) => (Token, UserName, Role) = (r.Token, r.UserName, r.Role);

    public void Clear() => (Token, UserName, Role) = (null, null, null);
}

public static class Fmt
{
    public static string Hours(double? v) => v is null ? "—" : $"{v:0.0} h";
    public static string Pct(double? v) => v is null ? "—" : $"{v * 100:0.00} %";
}
