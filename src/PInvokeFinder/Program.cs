using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PInvokeFinder
{
    public class Program
    {
        static List<string> paths;
        static List<string> pinvokes;

        public static void Main(string[] args)
        {
            ShowHeader();
            if (!ParseArguments(args))
            {
                ShowUsage();
                return;
            }

            pinvokes = new List<string>();
            foreach (var path in paths)
            {
                Console.WriteLine($"Analyzing {path}...");
                AnalyzePath(path);
            }

            Console.WriteLine();
            Console.WriteLine($"Done. Found {pinvokes.Count} p/invokes");
            Console.WriteLine();
            foreach (var m in pinvokes)
            {
                Console.WriteLine(m);
            }
        }

        private static void AnalyzePath(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var peFile = new PEReader(stream))
                {
                    var mr = peFile.GetMetadataReader();

                    foreach (var methodHandle in mr.MethodDefinitions)
                    {
                        var method = mr.GetMethodDefinition(methodHandle);
                        if (((method.Attributes & MethodAttributes.PinvokeImpl) == MethodAttributes.PinvokeImpl) ||
                                ((method.Attributes & MethodAttributes.UnmanagedExport) == MethodAttributes.UnmanagedExport))
                        {
                            pinvokes.Add($"[{path}]\t{GetMethodName(method, mr)}");
                        }
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine($"WARNING: {path} does not appear to be a valid managed assembly");
            }
        }

        private static string GetMethodName(MethodDefinition method, MetadataReader mr)
        {
            var nameComponents = new List<string>();
            if (method.Name != null)
            {
                nameComponents.Add(mr.GetString(method.Name));
            }

            var declTypeHandle = method.GetDeclaringType();
            TypeDefinition declType = mr.GetTypeDefinition(declTypeHandle);
            while (!declTypeHandle.IsNil)
            {
                declType = mr.GetTypeDefinition(declTypeHandle);
                nameComponents.Add(mr.GetString(declType.Name));
                declTypeHandle = declType.GetDeclaringType();
            }
            if (declType.Namespace!= null) nameComponents.Add(mr.GetString(declType.Namespace));
            return string.Join(".", nameComponents.Where(s => !string.IsNullOrWhiteSpace(s)).Reverse());
        }

        private static bool ParseArguments(string[] args)
        {
            paths = new List<string>();

            if (args.Length < 1)
            {
                return false;
            }

            foreach (string a in args)
            {
                if (File.Exists(a))
                {
                    paths.Add(a);
                }
                else if (Directory.Exists(a))
                {
                    paths.AddRange(Directory.GetFiles(a, "*.dll", SearchOption.AllDirectories));
                    paths.AddRange(Directory.GetFiles(a, "*.exe", SearchOption.AllDirectories));
                }
                else
                {
                    Console.WriteLine($"WARNING: Path not found {a}");
                }
            }

            if (paths.Count < 1)
            {
                Console.WriteLine("ERROR: No files found to analyze");
                return false;
            }

            return true;
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Usage: PInvokeFinder.exe <path to binary or directory>");
        }

        private static void ShowHeader()
        {
            Console.WriteLine("-------------------");
            Console.WriteLine("- P/Invoke Finder -");
            Console.WriteLine("-------------------");
            Console.WriteLine();
        }
    }
}
