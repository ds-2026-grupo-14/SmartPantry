using System;

namespace SmartPantry.Authors;

[Serializable]
public class AuthorExcelDownloadTokenCacheItem
{
    public string Token { get; set; } = string.Empty;
}
