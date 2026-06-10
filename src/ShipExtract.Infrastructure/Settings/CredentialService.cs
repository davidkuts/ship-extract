using System.Net;
using AdysTech.CredentialManager;

namespace ShipExtract.Infrastructure.Settings;

/// <summary>Contract for securely storing and retrieving the Anthropic API key.</summary>
public interface ICredentialService
{
    /// <summary>Retrieves the stored API key from Windows Credential Manager, or <see langword="null"/> if not set.</summary>
    string? GetApiKey();

    /// <summary>Saves the API key to Windows Credential Manager.</summary>
    void SaveApiKey(string apiKey);

    /// <summary>Removes the API key from Windows Credential Manager.</summary>
    void DeleteApiKey();
}

/// <summary>Implements <see cref="ICredentialService"/> using Windows Credential Manager.</summary>
public sealed class CredentialService : ICredentialService
{
    private const string TargetName = "ShipExtract_AnthropicApiKey";

    /// <inheritdoc/>
    public string? GetApiKey()
    {
        try
        {
            var cred = CredentialManager.GetCredentials(TargetName);
            return string.IsNullOrEmpty(cred?.Password) ? null : cred.Password;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void SaveApiKey(string apiKey)
    {
        try
        {
            CredentialManager.SaveCredentials(TargetName,
                new NetworkCredential("ShipExtract", apiKey));
        }
        catch
        {
            // Best-effort — caller should inform the user via UI if needed
        }
    }

    /// <inheritdoc/>
    public void DeleteApiKey()
    {
        try
        {
            CredentialManager.RemoveCredentials(TargetName);
        }
        catch
        {
            // Best-effort
        }
    }
}
