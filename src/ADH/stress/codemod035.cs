using System;
using System.IO;
using AestikModLoader.Runtime;

public sealed class StressMod : IAestikMod
{
    public void Initialize(ModContext context)
    {
        File.WriteAllText(Path.Combine(context.ModsRoot, "marker_codemod035.txt"), DateTime.UtcNow.ToString("o"));
    }

    public void Shutdown()
    {
    }
}
