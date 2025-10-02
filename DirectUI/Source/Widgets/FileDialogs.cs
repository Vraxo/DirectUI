using System;
using System.Collections.Generic;
using NativeFileDialogs.Net;

namespace DirectUI;

/// <summary>
/// Provides cross-platform native file dialogs using NativeFileDialog.NET.
/// </summary>
public static class FileDialogs
{
    /// <summary>
    /// Opens a native dialog for selecting a single file.
    /// </summary>
    /// <param name="filters">A dictionary of filters, where the key is the description (e.g., "Image Files") and the value is a comma-separated list of extensions (e.g., "png,jpg").</param>
    /// <param name="defaultPath">The default path to open the dialog in.</param>
    /// <returns>The path of the selected file, or null if the dialog was canceled or an error occurred.</returns>
    public static string? OpenFile(IDictionary<string, string>? filters = null, string? defaultPath = null)
    {
        try
        {
            NfdStatus result = Nfd.OpenDialog(out string? outPath, filters, defaultPath);

            switch (result)
            {
                case NfdStatus.Ok:
                    return outPath;
                case NfdStatus.Cancelled:
                    return null;
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NativeFileDialog] An exception occurred: {ex.Message}");
            return null;
        }
    }
}