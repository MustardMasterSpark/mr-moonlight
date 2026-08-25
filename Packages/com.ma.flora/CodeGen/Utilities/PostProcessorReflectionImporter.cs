// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace MA.Flora.CodeGen
{
    internal class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition module)
        {
            return new PostProcessorReflectionImporter(module);
        }
    }

    internal class PostProcessorReflectionImporter : DefaultReflectionImporter
    {
        private const string SystemPrivateCoreLib = "System.Private.CoreLib";

        private AssemblyNameReference m_CorrectCoreLib;

        public PostProcessorReflectionImporter(ModuleDefinition module) : base(module)
        {
            m_CorrectCoreLib = module.AssemblyReferences.FirstOrDefault(a => a.Name is "mscorlib" or "netstandard" or SystemPrivateCoreLib);
        }

        public override AssemblyNameReference ImportReference(AssemblyName reference)
        {
            if (m_CorrectCoreLib != null && reference.Name == SystemPrivateCoreLib)
                return m_CorrectCoreLib;

            return base.ImportReference(reference);
        }
    }
}
