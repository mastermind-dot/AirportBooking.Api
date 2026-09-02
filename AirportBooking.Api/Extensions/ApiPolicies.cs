namespace AirportBooking.Api.Extensions;

/// <summary>
/// Named policies, in one place so a controller attribute and the pipeline
/// registration cannot drift apart over a typo — a mismatch fails silently,
/// leaving the endpoint unprotected rather than erroring.
/// </summary>
public static class ApiPolicies
{
    public const string AuthRateLimit = "auth";
    public const string SearchRateLimit = "search";
    public const string FrontendCors = "FrontendOnly";
}
