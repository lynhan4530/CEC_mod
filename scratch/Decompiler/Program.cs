using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace DecompilerApp
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  Decompiler list <assemblyPath> [pattern]");
                Console.WriteLine("  Decompiler search <assemblyPath> <text>");
                Console.WriteLine("  Decompiler decompile <assemblyPath> <typeName> <outputPath>");
                return;
            }

            string cmd = args[0].ToLower();
            string assemblyPath = args[1];

            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"File not found: {assemblyPath}");
                return;
            }

            try
            {
                var settings = new DecompilerSettings();
                
                // Let's create an assembly resolver
                var resolver = new UniversalAssemblyResolver(assemblyPath, false, "net8.0");
                resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));
                
                var decompiler = new CSharpDecompiler(assemblyPath, resolver, settings);

                if (cmd == "list")
                {
                    string pattern = args.Length > 2 ? args[2] : "";
                    var peFile = new PEFile(assemblyPath);
                    var typeSystem = new DecompilerTypeSystem(peFile, resolver);
                    var types = typeSystem.MainModule.TypeDefinitions;
                    
                    Console.WriteLine($"Listing types matching '{pattern}':");
                    int count = 0;
                    foreach (var t in types)
                    {
                        if (string.IsNullOrEmpty(pattern) || t.FullName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine(t.FullName);
                            count++;
                        }
                    }
                    Console.WriteLine($"Total: {count} types.");
                }
                else if (cmd == "search")
                {
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Please specify search text.");
                        return;
                    }
                    string searchText = args[2];
                    var peFile = new PEFile(assemblyPath);
                    var typeSystem = new DecompilerTypeSystem(peFile, resolver);
                    var types = typeSystem.MainModule.TypeDefinitions;
                    
                    Console.WriteLine($"Searching for '{searchText}' in {assemblyPath}...");
                    int count = 0;
                    foreach (var t in types)
                    {
                        if (t.Name == "<Module>") continue;
                        try
                        {
                            string code = decompiler.DecompileTypeAsString(t.FullTypeName);
                            if (code.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"Found in: {t.FullName}");
                                count++;
                            }
                        }
                        catch (Exception)
                        {
                            // Some compiler generated types might fail to decompile individually
                        }
                    }
                    Console.WriteLine($"Finished. Found in {count} types.");
                }
                else if (cmd == "decompile")
                {
                    if (args.Length < 4)
                    {
                        Console.WriteLine("Usage: Decompiler decompile <assemblyPath> <typeName> <outputPath>");
                        return;
                    }
                    string typeNameStr = args[2];
                    string outputPath = args[3];

                    var peFile = new PEFile(assemblyPath);
                    var typeSystem = new DecompilerTypeSystem(peFile, resolver);
                    var typeDef = typeSystem.MainModule.TypeDefinitions.FirstOrDefault(t => t.FullName.Equals(typeNameStr, StringComparison.OrdinalIgnoreCase));
                    
                    if (typeDef == null)
                    {
                        // Try case-insensitive substring match
                        typeDef = typeSystem.MainModule.TypeDefinitions.FirstOrDefault(t => t.FullName.Contains(typeNameStr, StringComparison.OrdinalIgnoreCase));
                    }

                    if (typeDef == null)
                    {
                        Console.WriteLine($"Type '{typeNameStr}' not found.");
                        return;
                    }

                    Console.WriteLine($"Decompiling {typeDef.FullName} to {outputPath}...");
                    string code = decompiler.DecompileTypeAsString(typeDef.FullTypeName);
                    File.WriteAllText(outputPath, code);
                    Console.WriteLine("Done.");
                }
                else
                {
                    Console.WriteLine($"Unknown command: {cmd}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
