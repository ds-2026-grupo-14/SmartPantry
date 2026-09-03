using System;
using SmartPantry.Entities.Books;

namespace SmartPantry.Services.Dtos.Books;

public class BookExcelDto
{
    public string Name { get; set; }

    public string AuthorName { get; set; }

    public BookType Type { get; set; }

    public DateTime PublishDate { get; set; }

    public float Price { get; set; }
}
