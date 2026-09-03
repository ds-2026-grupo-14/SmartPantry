namespace SmartPantry.Services.Dtos.Authors;

public class AuthorExcelDownloadDto
{
    public string DownloadToken { get; set; } = string.Empty;

    public string? Sorting { get; set; }
}
