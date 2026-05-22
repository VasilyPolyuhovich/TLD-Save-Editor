using System.Diagnostics;
using System.Text.Json;
using The_Long_Dark_Save_Editor_2.Services;

const string url = "http://127.0.0.1:5173";

var builder = WebApplication.CreateBuilder(args);

// Local-only tool: bind to loopback so the editor is never exposed on the network.
builder.WebHost.UseUrls(url);

// One in-memory editing session is enough for a single-user local tool.
builder.Services.AddSingleton<SaveEditorService>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/saves-folder", (SaveEditorService svc) => Results.Ok(new { folder = svc.SavesFolder }));

api.MapPost("/saves-folder", (SaveEditorService svc, FolderRequest req) =>
{
    if (!string.IsNullOrWhiteSpace(req.Folder))
        svc.SavesFolder = req.Folder;
    return Results.Ok(new { folder = svc.SavesFolder });
});

api.MapGet("/saves", (SaveEditorService svc) => Guard(() => Results.Ok(svc.ListSaves())));

api.MapPost("/load", (SaveEditorService svc, LoadRequest req) =>
    Guard(() => Results.Ok(svc.Load(req.Path))));

api.MapGet("/player", (SaveEditorService svc) => Guard(() => Results.Ok(svc.GetPlayer())));

api.MapPut("/player", (SaveEditorService svc, PlayerDto dto) =>
    Guard(() => { svc.UpdatePlayer(dto); return Results.Ok(); }));

api.MapGet("/inventory", (SaveEditorService svc) => Guard(() => Results.Ok(svc.GetInventory())));

api.MapPost("/inventory/add", (SaveEditorService svc, AddItemRequest req) =>
    Guard(() => { svc.AddItem(req.PrefabName); return Results.Ok(); }));

api.MapPut("/inventory/{instanceId:int}", (SaveEditorService svc, int instanceId, UpdateItemRequest req) =>
    Guard(() => { svc.UpdateItem(instanceId, req); return Results.Ok(); }));

api.MapDelete("/inventory/{instanceId:int}", (SaveEditorService svc, int instanceId) =>
    Guard(() => { svc.RemoveItem(instanceId); return Results.Ok(); }));

api.MapGet("/items", (SaveEditorService svc) => Guard(() => Results.Ok(svc.GetAvailableItems())));

api.MapGet("/skills", (SaveEditorService svc) => Guard(() => Results.Ok(svc.GetSkills())));

api.MapPut("/skills", (SaveEditorService svc, SkillsDto dto) =>
    Guard(() => { svc.UpdateSkills(dto); return Results.Ok(); }));

api.MapGet("/afflictions", (SaveEditorService svc) => Guard(() => Results.Ok(svc.GetAfflictions())));

api.MapPost("/afflictions/remove", (SaveEditorService svc, RemoveAfflictionRequest req) =>
    Guard(() => { svc.RemoveAffliction(req.Positive, req.Index); return Results.Ok(); }));

api.MapPost("/afflictions/cure-all", (SaveEditorService svc) =>
    Guard(() => { svc.CureAllAfflictions(); return Results.Ok(); }));

api.MapGet("/map", (SaveEditorService svc) => Guard(() => Results.Ok(svc.GetMap())));

api.MapPost("/map/position", (SaveEditorService svc, MapPositionRequest req) =>
    Guard(() => { svc.SetMapPosition(req.X, req.Y); return Results.Ok(); }));

api.MapGet("/profile", (SaveEditorService svc) => Guard(() => Results.Ok(svc.GetProfile())));

api.MapPut("/profile", (SaveEditorService svc, ProfileDto dto) =>
    Guard(() => { svc.UpdateProfile(dto); return Results.Ok(); }));

api.MapGet("/backups", (SaveEditorService svc) => Guard(() => Results.Ok(svc.ListBackups())));

api.MapPost("/restore", (SaveEditorService svc, RestoreRequest req) =>
    Guard(() => Results.Ok(svc.RestoreBackup(req.BackupPath))));

api.MapPost("/save", (SaveEditorService svc) => Guard(() => { svc.Save(); return Results.Ok(); }));

Console.WriteLine($"TLD Save Editor — {url}");

// Open the default browser on startup unless told not to (e.g. headless / CI).
var openBrowser = !args.Contains("--no-browser")
    && !string.Equals(Environment.GetEnvironmentVariable("TLD_NO_BROWSER"), "1");
if (openBrowser)
    app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(url));

app.Run();

static void OpenBrowser(string target)
{
    try
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        else if (OperatingSystem.IsMacOS())
            Process.Start("open", target);
        else
            Process.Start("xdg-open", target);
    }
    catch { /* no browser available (headless) — the URL is printed above */ }
}

// Maps service exceptions to appropriate status codes. Known/expected exceptions carry a
// safe, user-facing message; anything unexpected returns a generic 500 (the detail is logged
// server-side, not echoed to the client).
static IResult Guard(Func<IResult> action)
{
    try { return action(); }
    catch (FileNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex);
        return Results.Problem("Unexpected error while processing the save.", statusCode: 500);
    }
}

record FolderRequest(string Folder);
record LoadRequest(string Path);
record AddItemRequest(string PrefabName);
record RemoveAfflictionRequest(bool Positive, int Index);
