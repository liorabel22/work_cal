using MauiDbManager.Models;
using MauiDbManager.Parsing;
using MauiDbManager.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IDatabaseConnectionService, SqlServerConnectionService>();
builder.Services.AddSingleton<IDatabaseLookupService, SqlServerLookupService>();
builder.Services.AddSingleton<IDatabaseModificationService, SqlServerModificationService>();
var app = builder.Build();
app.MapGet("/api/health", () => Results.Ok(new { name = "lior_cal", status = "ok" }));
app.MapPost("/api/connect", async (Credentials c, IDatabaseConnectionService service, CancellationToken ct) =>
{
    if (!c.Valid()) return Results.BadRequest(new { message = "Server and username are required." });
    return Results.Ok(await service.TestConnectionAsync(c.Server.Trim(), c.User.Trim(), c.Password, false, ct));
});
app.MapPost("/api/database", async (DatabaseRequest r, IDatabaseLookupService service, CancellationToken ct) =>
{
    if ((r.Connection is null || !r.Connection.Valid()) || string.IsNullOrWhiteSpace(r.Database)) return Results.BadRequest(new { message = "Select a database and provide connection details." });
    return Results.Ok(await service.LoadTypeBOptionsAsync(r.Connection.Server.Trim(), r.Database, r.Connection.User.Trim(), r.Connection.Password, false, ct));
});
app.MapPost("/api/{action}", async (string action, ChangeRequest r, IDatabaseModificationService service, CancellationToken ct) =>
{
    if (action is not ("preview" or "apply")) return Results.NotFound();
    try
    {
        if ((r.Connection is null || !r.Connection.Valid()) || string.IsNullOrWhiteSpace(r.Database)) throw new ArgumentException("Connection and database are required.");
        var statuses = new List<TableStatusModification>();
        if (r.Gain is not null) statuses.Add(new("dbo.gain", Boolean(r.Gain.Value)));
        if (r.Zakan is not null) statuses.Add(new("dbo.zakan", Boolean(r.Zakan.Value)));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        OverallModification? overall = null;
        AmplitudeModification? amplitude = null;
        if (r.Overall is { } o)
        {
            if (!o.UpdateStatus && !o.UpdateTime) throw new ArgumentException("Overall: select status or time.");
            if (o.Status < 0 || o.Status > 3) throw new ArgumentException("Invalid overall status.");
            if (o.TypeB is null || (!o.All && o.TypeB.Count == 0)) throw new ArgumentException("Select at least one TB option.");
            overall = new(Ids(o.MidIds), Ids(o.HighIds), o.All, o.TypeB.ToHashSet(), o.UpdateStatus, (OverallStatus)o.Status, o.UpdateTime, o.Time ?? now);
        }
        if (r.Amplitude is { } a)
        {
            if (!a.UpdateStatus && !a.UpdateTime) throw new ArgumentException("Amplitude: select status or time.");
            amplitude = new(Ids(a.LowIds), a.UpdateStatus, Boolean(a.Status), a.UpdateTime, a.Time ?? now);
        }
        if (statuses.Count == 0 && overall is null && amplitude is null) throw new ArgumentException("Enable at least one changed section.");
        var c = r.Connection;
        if (action == "preview") return Results.Ok(await service.PreviewModificationsAsync(c.Server.Trim(), r.Database, c.User.Trim(), c.Password, false, statuses, overall, amplitude, ct));
        return Results.Ok(await service.ApplyModificationsAsync(c.Server.Trim(), r.Database, c.User.Trim(), c.Password, false, statuses, overall, amplitude, ct));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
});
app.Run();
static IReadOnlyList<long> Ids(string value)
{
    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Enter at least one ID.");
    if (value.Length > 100_000) throw new ArgumentException("ID selector is too long.");
    var result = LowIdSelectorParser.Parse(value);
    if (!result.IsSuccess || result.Values.Count > 10_000) throw new ArgumentException(result.ErrorMessage ?? "Select at most 10,000 IDs.");
    return result.Values;
}
static BooleanStatus Boolean(int value) => value is 0 or 1 ? (BooleanStatus)value : throw new ArgumentException("Boolean status must be 0 or 1.");
record Credentials(string Server, string User, string Password) { public bool Valid() => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(User) && Password is not null; }
record DatabaseRequest(Credentials Connection, string Database);
record OverallInput(string MidIds, string HighIds, bool All, List<long> TypeB, bool UpdateStatus, int Status, bool UpdateTime, long? Time);
record AmplitudeInput(string LowIds, bool UpdateStatus, int Status, bool UpdateTime, long? Time);
record ChangeRequest(Credentials Connection, string Database, int? Gain, int? Zakan, OverallInput? Overall, AmplitudeInput? Amplitude);
