using System;
using System.IO;
using System.Reflection;
internal static class Program
{
    static string gameRoot = @""C:\Program Files (x86)\Steam\steamapps\common\Aestik"";
    static string managedRoot = Path.Combine(gameRoot, "Aestik_Data", "Managed");
    static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        string runtimePath = Path.Combine(managedRoot, "AestikModLoader.Runtime.dll");
        Assembly asm = Assembly.LoadFrom(runtimePath);
        Type loader = asm.GetType("AestikModLoader.Runtime.ModLoader");
        loader.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    }
    static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string simple = new AssemblyName(args.Name).Name + ".dll";
        string candidate = Path.Combine(managedRoot, simple);
        return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
    }
}
