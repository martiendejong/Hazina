namespace Hazina.Tools.Services.DataGathering.Models;

/// <summary>
/// Specifies the type of data stored in a <see cref="GatheredDataValue"/>.
/// </summary>
public enum GatheredDataValueType
{
    /// <summary>
    /// A short string value suitable for inline display.
    /// </summary>
    String,

    /// <summary>
    /// A numeric value (integer or decimal).
    /// </summary>
    Number,

    /// <summary>
    /// A larger text value suitable for display in a textarea or expandable section.
    /// </summary>
    LargeText,

    /// <summary>
    /// A collection of nested <see cref="GatheredDataItem"/> values.
    /// </summary>
    List
}
