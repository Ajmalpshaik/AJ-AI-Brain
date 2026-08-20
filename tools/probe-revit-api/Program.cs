// Print the real public members of any Revit API type, for ANY Revit version - including the .NET-based
// ones (2025+) that Windows PowerShell 5.1 physically cannot load.
//
// WHY THIS EXISTS: on 2026-08-20, "does Revit 2027 still have Zone.AddSpaces?" could not be answered.
// [Reflection.Assembly]::LoadFrom on 2027's RevitAPI.dll throws BadImageFormatException, because
// PowerShell 5.1 runs on .NET Framework and cannot load a .NET 10 assembly. Guessing the replacement
// API is exactly the habit this Brain exists to stop, so this reads the metadata instead.
// It answered the question in one run: 2027 has NO zone method left on Creation.Document, no
// Zone.AddSpaces, and a read-only Space.Zone - i.e. the capability is gone, not renamed.
//
// MetadataLoadContext only READS metadata - it never executes Revit code, so Revit need not be running
// and nothing is loaded into a live process.
//
// USAGE (from this folder):
//   dotnet run -v q -- 2027 Autodesk.Revit.DB.Mechanical.Zone
//   dotnet run -v q -- 2024 Autodesk.Revit.DB.IndependentTag Autodesk.Revit.DB.FormatOptions
//   dotnet run -v q -- "C:\Program Files\Autodesk\Revit 2027" Autodesk.Revit.DB.Floor
// The first argument is a Revit year or a full install folder; the rest are fully-qualified type names.
using System;
using System.IO;
using System.Linq;
using System.Reflection;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("usage: dotnet run -- <revit-year|install-folder> <Full.Type.Name> [more types...]");
            return 1;
        }

        string dir = args[0];
        if (!Directory.Exists(dir)) dir = @"C:\Program Files\Autodesk\Revit " + args[0];
        if (!Directory.Exists(dir)) { Console.WriteLine("No such Revit folder: " + dir); return 2; }

        string api = Path.Combine(dir, "RevitAPI.dll");
        if (!File.Exists(api)) { Console.WriteLine("No RevitAPI.dll in " + dir); return 2; }

        // Resolve against Revit's own DLLs plus the running .NET's, so base types resolve too.
        var files = Directory.GetFiles(dir, "*.dll").ToList();
        files.AddRange(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll"));

        using var ctx = new MetadataLoadContext(new PathAssemblyResolver(files));
        var asm = ctx.LoadFromAssemblyPath(api);
        Console.WriteLine("Revit    : " + dir);
        Console.WriteLine("RevitAPI : " + asm.GetName().Version);
        Console.WriteLine();

        foreach (var typeName in args.Skip(1))
        {
            var t = asm.GetType(typeName);
            if (t == null) { Console.WriteLine("!! type not found: " + typeName); Console.WriteLine(); continue; }

            Console.WriteLine("=== " + typeName + " ===");
            foreach (var m in t.GetMethods().Where(m => m.IsPublic && !m.IsSpecialName).OrderBy(m => m.Name))
                Console.WriteLine("  " + m.Name + "(" +
                    string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name)) + ") -> " + m.ReturnType.Name);
            foreach (var pr in t.GetProperties().OrderBy(p => p.Name))
                Console.WriteLine("  ." + pr.Name + " : " + pr.PropertyType.Name +
                    (pr.CanRead ? " get" : "") + (pr.CanWrite ? " set" : ""));
            Console.WriteLine();
        }
        return 0;
    }
}
