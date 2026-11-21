namespace Akr.Core.Configuration;

public interface IVersionConfig
{
    string? GetVersion(string packageName);
    IReadOnlyDictionary<string, string> GetAll();
}
