namespace Netcorext.Auth.Authentication.Services.Permission.Queries.Models;

public class PermissionCondition
{
    public long PermissionId { get; set; }
    public int Priority { get; set; }
    public string? Group { get; set; }
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public bool Allowed { get; set; }
}