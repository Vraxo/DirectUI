using NativeFileDialogs.Net;

namespace DirectUI;

public static class FileDialogs
{
    public static string? OpenFile()
    {
        NfdStatus result = Nfd.OpenDialog(out string? outPath);

        return result switch
        {
            NfdStatus.Ok => outPath,
            NfdStatus.Cancelled => null,
            _ => null,
        };
    }
}