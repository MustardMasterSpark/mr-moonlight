// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable AssignNullToNotNullAttribute

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace MA.Flora.CodeGen
{
    internal class ILPostProcessManager : ILPostProcessor
    {
        private static readonly ILSubPostProcessor[] PostProcessors =
        {
            // new InternalsILSubPostProcessor(),
#if !FLORA_DISABLE_SHADER_GRAPH_INJECTION
            new ShaderGraphILSubPostProcessor(),
            new UniversalILSubPostProcessor()
#endif
        };

        public override ILPostProcessor GetInstance()
            => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
            => PostProcessors.Any(p => p.WillProcessAssembly(compiledAssembly));

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            var diagnostics = new List<DiagnosticMessage>();
            var assemblyDefinition = CodeGenUtility.AssemblyDefinitionFor(compiledAssembly);
            var hasAnyChanges = false;

            foreach (ILSubPostProcessor postProcessor in PostProcessors)
            {
                if (postProcessor.WillProcessAssembly(compiledAssembly))
                {
                    diagnostics.AddRange(postProcessor.PostProcess(assemblyDefinition, out bool didChange));
                    hasAnyChanges |= didChange;
                }
            }

            // Hack to remove circular references
            var selfName = assemblyDefinition.Name.FullName;
            foreach (AssemblyNameReference referenceName in assemblyDefinition.MainModule.AssemblyReferences)
            {
                if (referenceName.FullName == selfName)
                {
                    assemblyDefinition.MainModule.AssemblyReferences.Remove(referenceName);
                    break;
                }
            }

            if (!hasAnyChanges || diagnostics.Any(d => d.DiagnosticType == DiagnosticType.Error))
                return new ILPostProcessResult(null, diagnostics);

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };
            assemblyDefinition.Write(pe, writerParameters);

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), diagnostics);
        }
    }

    internal abstract class ILSubPostProcessor
    {
        protected const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public IEnumerable<DiagnosticMessage> PostProcess(AssemblyDefinition assemblyDefinition, out bool didChange)
        {
            try
            {
                didChange = PostProcessAssemblyDefinition(assemblyDefinition);
            }
            catch (FoundErrorInUserCodeException e)
            {
                didChange = false;
                return e.DiagnosticMessages;
            }

            return DiagnosticMessages;
        }

        public abstract bool WillProcessAssembly(ICompiledAssembly compiledAssembly);

        protected List<DiagnosticMessage> DiagnosticMessages = new List<DiagnosticMessage>();

        protected abstract bool PostProcessAssemblyDefinition(AssemblyDefinition assemblyDefinition);
    }
}
