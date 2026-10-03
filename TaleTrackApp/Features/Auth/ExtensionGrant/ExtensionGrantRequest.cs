using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Features.Auth.ExtensionGrant;

/// <summary>Optional body of <c>POST /api/auth/extension-grant</c>.</summary>
public class ExtensionGrantRequest
{
    /// <summary>Label of the extension's session in the sessions list (e.g. "Prime Video extension"). Defaults to "Netflix extension" when empty.</summary>
    [StringLength(60, ErrorMessage = "The device name cannot exceed 60 characters")]
    public string? Device { get; set; }
}
