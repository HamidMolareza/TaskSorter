using Microsoft.AspNetCore.DataProtection;

namespace TaskSorter.Backend.Security;

public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("TaskSorter.GitHubToken.v1");

    public string Protect(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : _protector.Protect(value);

    public string Unprotect(string protectedValue) =>
        string.IsNullOrEmpty(protectedValue) ? string.Empty : _protector.Unprotect(protectedValue);
}
