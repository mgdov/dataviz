namespace DataViz.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "DataViz";
    public string Audience { get; set; } = "DataViz.Web";
    public string Key { get; set; } = string.Empty;
    public int ExpireMinutes { get; set; } = 480;
}
