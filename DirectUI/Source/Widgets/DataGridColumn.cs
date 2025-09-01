// DirectUI/Source/Widgets/DataGridColumn.cs
namespace DirectUI;

/// <summary>
/// Defines a column for the DataGrid control.
/// </summary>
public class DataGridColumn
{
    /// <summary>
    /// The text displayed in the column header.
    /// </summary>
    public string HeaderText { get; }

    /// <summary>
    /// The initial width of the column. This can be changed by the user resizing the column.
    /// </summary>
    public float InitialWidth { get; }

    /// <summary>
    /// The name of the public property on the data item object from which to retrieve the cell value.
    /// </summary>
    public string DataPropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataGridColumn"/> class.
    /// </summary>
    /// <param name="headerText">The text to display in the column header.</param>
    /// <param name="initialWidth">The initial width of the column.</param>
    /// <param name="dataPropertyName">The name of the public property to bind to.</param>
    public DataGridColumn(string headerText, float initialWidth, string dataPropertyName)
    {
        HeaderText = headerText;
        InitialWidth = initialWidth;
        DataPropertyName = dataPropertyName;
    }
}