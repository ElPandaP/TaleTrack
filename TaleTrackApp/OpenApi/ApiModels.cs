namespace TaleTrackApp.OpenApi;

/// <summary>
/// Body of a failed request. Only <see cref="Message"/> is always present; <see cref="Success"/> and
/// <see cref="Code"/> appear on the endpoints that document them.
/// </summary>
public class ApiError
{
    /// <summary>Always <c>false</c> when present.</summary>
    public bool? Success { get; set; }

    /// <summary>Stable machine-readable reason (e.g. <c>email_taken</c>, <c>invalid_or_expired</c>). Clients map it to localized text.</summary>
    public string? Code { get; set; }

    /// <summary>Human-readable explanation in English. Validation failures join every violated rule with "; ".</summary>
    public required string Message { get; set; }
}

/// <summary>Body of an operation that succeeds without returning data.</summary>
public class ApiResult
{
    /// <summary>Always <c>true</c>.</summary>
    public bool Success { get; set; }

    /// <summary>Machine-readable outcome, only on endpoints that document one.</summary>
    public string? Code { get; set; }

    /// <summary>Human-readable confirmation in English.</summary>
    public string? Message { get; set; }
}
