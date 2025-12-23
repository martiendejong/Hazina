using System.Collections.Generic;

/// <summary>
/// Model dat wordt gebruikt voor het structureren en opslaan van blogcategorieën in de applicatie.
/// </summary>
public class BlogCategory
{
    /// <summary>
    /// Unieke identificatie voor de categorie.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Naam van de blogcategorie.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Korte omschrijving van de blogcategorie.
    /// </summary>
    public string Description { get; set; }
}

/// <summary>
/// Een lijst van blogcategorieën.
/// </summary>
public class BlogCategoryList : List<BlogCategory> { }
