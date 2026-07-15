namespace HermesNotifier.Api.DTOs.Requests.Admin;

public class AdminLoginRequest
{
    public string? IdToken { get; set; }

    public string? Code { get; set; }

    public string? RedirectUri { get; set; }
}
