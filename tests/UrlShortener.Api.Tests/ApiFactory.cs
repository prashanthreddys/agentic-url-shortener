using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Driver;

namespace UrlShortener.Api.Tests;

/// <summary>
/// Hosts the real API in-process for HTTP integration tests against a locally running MongoDB
/// (mongodb://localhost:27017), using a throwaway database that is dropped when the factory is
/// disposed. No Docker required; a native MongoDB service must be running on port 27017.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string ConnectionString = "mongodb://localhost:27017";
    private readonly string _databaseName = "urlshortener_test_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // UseSetting is applied before the app reads configuration, so it reliably isolates each run.
        builder.UseSetting("ConnectionStrings:Mongo", ConnectionString);
        builder.UseSetting("Persistence:Database", _databaseName);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try { new MongoClient(ConnectionString).DropDatabase(_databaseName); }
            catch { /* best-effort cleanup */ }
        }
    }
}
