using System.Security.Cryptography;
using System.Text;

namespace UrlShortener.Orchestration.Execution;

/// <summary>
/// An immutable output produced by a stage agent (code, schema, test report, design doc, ...).
/// The <see cref="Fingerprint"/> lets the orchestrator detect when an upstream output changed and
/// downstream work must be re-planned.
/// </summary>
public sealed class Artifact
{
    public string Name { get; }
    public string Kind { get; }
    public string Summary { get; }
    public string Content { get; }

    public Artifact(string name, string kind, string summary, string content)
    {
        Name = name;
        Kind = kind;
        Summary = summary;
        Content = content;
    }

    public string Fingerprint
    {
        get
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Content));
            return Convert.ToHexString(bytes)[..12];
        }
    }
}
