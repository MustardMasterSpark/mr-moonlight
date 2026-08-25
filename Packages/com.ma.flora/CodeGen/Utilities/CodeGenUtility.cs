// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace MA.Flora.CodeGen
{
    internal static class CodeGenUtility
    {
        public static AssemblyDefinition AssemblyDefinitionFor(ICompiledAssembly compiledAssembly)
        {
            var resolver = new PostProcessorAssemblyResolver(compiledAssembly);
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = resolver,
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, readerParameters);

            //apparently, it will happen that when we ask to resolve a type that lives inside Unity.Entities, and we
            //are also postprocessing Unity.Entities, type resolving will fail, because we do not actually try to resolve
            //inside the assembly we are processing. Let's make sure we do that, so that we can use postprocessor features inside
            //unity.entities itself as well.
            resolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);

            return assemblyDefinition;
        }

        public static ILPostProcessResult GetResult(AssemblyDefinition assemblyDefinition, List<DiagnosticMessage> diagnostics)
        {
            MemoryStream pe = new MemoryStream();
            MemoryStream pdb = new MemoryStream();

            WriterParameters writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream = pdb,
                WriteSymbols = true
            };

            assemblyDefinition.Write(pe, writerParameters);

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), diagnostics);
        }

        public static string UnityEngineDLLDirectoryName()
        {
            var directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            return directoryName?.Replace(@"file:\", "");
        }

        public static ISymbolReaderProvider GetSymbolReaderProvider(string inputFile)
        {
            string nakedFileName = inputFile[..^4];
            if (File.Exists(nakedFileName + ".pdb"))
            {
                Console.WriteLine("Symbols will be read from " + nakedFileName + ".pdb");
                return new PdbReaderProvider();
            }
            if (File.Exists(nakedFileName + ".dll.mdb"))
            {
                Console.WriteLine("Symbols will be read from " + nakedFileName + ".dll.mdb");
                return new MdbReaderProvider();
            }
            Console.WriteLine("No symbols for " + inputFile);
            return null;
        }

        public static string DestinationFileFor(string outputDir, string assemblyPath)
        {
            var fileName = Path.GetFileName(assemblyPath);
            Debug.Assert(fileName != null, "fileName != null");
            return Path.Combine(outputDir, fileName);
        }

        public static string PrettyPrintType(TypeReference type)
        {
            // generic instances, such as List<Int32>
            if (type.IsGenericInstance)
            {
                var genericInstanceType = (GenericInstanceType)type;
                return genericInstanceType.Name[..^2] + "<" + string.Join(", ", genericInstanceType.GenericArguments.Select(PrettyPrintType).ToArray()) + ">";
            }

            // generic types, such as List<T>
            if (type.HasGenericParameters)
            {
                return type.Name[..^2] + "<" + string.Join(", ", type.GenericParameters.Select(x => x.Name).ToArray()) + ">";
            }

            // non-generic type such as Int
            return type.Name;
        }

        public static void AddWarning(this List<DiagnosticMessage> diagnostics, string message)
        {
            diagnostics.AddWarning((SequencePoint)null, message);
        }

        public static void AddWarning(this List<DiagnosticMessage> diagnostics, MethodDefinition methodDefinition, string message)
        {
            diagnostics.AddWarning(methodDefinition.DebugInformation.SequencePoints.FirstOrDefault(), message);
        }

        public static void AddWarning(this List<DiagnosticMessage> diagnostics, SequencePoint sequencePoint, string message)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                DiagnosticType = DiagnosticType.Warning,
                File = sequencePoint?.Document.Url.Replace($"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}", ""),
                Line = sequencePoint?.StartLine ?? 0,
                Column = sequencePoint?.StartColumn ?? 0,
                MessageData = $" - {message}"
            });
        }

        public static void AddError(this List<DiagnosticMessage> diagnostics, string message)
        {
            diagnostics.AddError((SequencePoint)null, message);
        }

        public static void AddError(this List<DiagnosticMessage> diagnostics, MethodDefinition methodDefinition, string message)
        {
            diagnostics.AddError(methodDefinition.DebugInformation.SequencePoints.FirstOrDefault(), message);
        }

        public static void AddError(this List<DiagnosticMessage> diagnostics, SequencePoint sequencePoint, string message)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                DiagnosticType = DiagnosticType.Error,
                File = sequencePoint?.Document.Url.Replace($"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}", ""),
                Line = sequencePoint?.StartLine ?? 0,
                Column = sequencePoint?.StartColumn ?? 0,
                MessageData = $" - {message}"
            });
        }
    }

    internal class FoundErrorInUserCodeException : Exception
    {
        public DiagnosticMessage[] DiagnosticMessages { get; }

        public FoundErrorInUserCodeException(DiagnosticMessage[] diagnosticMessages)
        {
            DiagnosticMessages = diagnosticMessages;
        }

        public override string ToString() => DiagnosticMessages.Select(dm => dm.MessageData).SeparateByComma();
    }

    internal static class ListExtensions
    {
        public static void Add<T>(this List<T> list, IEnumerable<T> elementsToAdd)
            => list.AddRange(elementsToAdd);
    }

    internal static class StringExtensions
    {
        public static string SeparateBy(this IEnumerable<string> elements, string delimiter)
        {
            bool first = true;
            var sb = new StringBuilder();
            foreach (var e in elements)
            {
                if (!first)
                    sb.Append(delimiter);
                sb.Append(e);
                first = false;
            }

            return sb.ToString();
        }

        public static string SeparateBySpace(this IEnumerable<string> elements) => elements.SeparateBy(" ");
        public static string SeparateByComma(this IEnumerable<string> elements) => elements.SeparateBy(",");
    }
}
