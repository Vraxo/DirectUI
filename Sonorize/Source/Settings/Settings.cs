using System.Collections.Generic;

namespace Sonorize;

public class Settings
{
    public List<string> Directories { get; set; } = new();
    public bool PlayOnDoubleClick { get; set; } = true;
}