using System;
using System.IO;
using System.Reflection;

internal static class Program
{
    private static string gameRoot = @"C:\Program Files (x86)\Steam\steamapps\common\Aestik";

    public static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        string managed = Path.Combine(gameRoot, "Aestik_Data", "Managed");
        string runtime = Path.Combine(managed, "ADH.Runtime.dll");
        if (!File.Exists(runtime))
        {
            runtime = Path.Combine(managed, "Aestifly.Runtime.dll");
        }
        if (!File.Exists(runtime))
        {
            runtime = Path.Combine(managed, "AestikModLoader.Runtime.dll");
        }
        Assembly asm = Assembly.LoadFrom(runtime);
        Type loader = asm.GetType("AestikModLoader.Runtime.ModLoader");
        loader.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    }

    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        string[] candidates = Directory.GetFiles(Path.Combine(gameRoot, "Aestik_Data", "ModLoader", "Mods"), name, SearchOption.AllDirectories);
        if (candidates.Length > 0)
        {
            return Assembly.LoadFrom(candidates[0]);
        }
        string managed = Path.Combine(gameRoot, "Aestik_Data", "Managed", name);
        if (File.Exists(managed))
        {
            return Assembly.LoadFrom(managed);
        }
        return null;
    }
}
