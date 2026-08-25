// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Linq;
using System.Reflection;
using MA.Flora.CodeGen.Forwarding;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace MA.Flora.CodeGen
{
    internal class UniversalILSubPostProcessor : ILSubPostProcessor
    {
        public override bool WillProcessAssembly(ICompiledAssembly compiledAssembly)
        {
            return compiledAssembly.Name == "Unity.RenderPipelines.Universal.Editor";
        }

        protected override bool PostProcessAssemblyDefinition(AssemblyDefinition assemblyDefinition)
        {
            bool didProcess = ProcessUniversalGetActiveBlocks(assemblyDefinition);
            didProcess |= ProcessUniversalGetActiveFields(assemblyDefinition);
            return didProcess;
        }

        private bool ProcessUniversalGetActiveFields(AssemblyDefinition assemblyDefinition)
        {
            TypeDefinition subTargetType = assemblyDefinition.MainModule.Types.FirstOrDefault(t => t.FullName == "UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget");
            if (subTargetType == null)
                return false;

            MethodDefinition getFieldsMethod = subTargetType.Methods.FirstOrDefault(m => m.Name == "GetFields");
            if (getFieldsMethod == null)
                return false;  // No-op if the method doesn't exist.

            MethodInfo forwardMethod = typeof(ShaderGraphEvents).GetMethods(AllMembers).FirstOrDefault(m => m.Name == "ForwardPreGetFields");
            if (forwardMethod == null)
                return false;  // No-op if that event function doesn't exist.

            // Import the method reference so we can call it
            MethodReference forwardMethodRef = assemblyDefinition.MainModule.ImportReference(forwardMethod);
            ILProcessor processor = getFieldsMethod.Body.GetILProcessor();
            Collection<Instruction> instructions = getFieldsMethod.Body.Instructions;
            getFieldsMethod.Body.InitLocals = true;

            Instruction firstInstruction = instructions[0];
            processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldarg_0)); // subTarget => this
            processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldarg_1)); // context
            processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldind_Ref));
            processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Call, forwardMethodRef)); // Call the pre-hook method

            return true;
        }

        private bool ProcessUniversalGetActiveBlocks(AssemblyDefinition assemblyDefinition)
        {
            TypeDefinition subTargetType = assemblyDefinition.MainModule.Types.FirstOrDefault(t => t.FullName == "UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget");
            if (subTargetType == null)
                return false;

            MethodDefinition getActiveBlocksMethod = subTargetType.Methods.FirstOrDefault(m => m.Name == "GetActiveBlocks");
            if (getActiveBlocksMethod == null)
                return false;  // No-op if the method doesn't exist.

            MethodInfo forwardMethod = typeof(ShaderGraphEvents).GetMethods(AllMembers).FirstOrDefault(m => m.Name == "ForwardPreGetActiveBlocks");
            if (forwardMethod == null)
                return false;  // No-op if that event function doesn't exist.

            // Import the method reference so we can call it
            MethodReference forwardMethodRef = assemblyDefinition.MainModule.ImportReference(forwardMethod);
            ILProcessor processor = getActiveBlocksMethod.Body.GetILProcessor();
            Collection<Instruction> instructions = getActiveBlocksMethod.Body.Instructions;
            getActiveBlocksMethod.Body.InitLocals = true;

            Instruction firstInstruction = instructions[0];
            processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldarg_0)); // subTarget => this
            processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldarg_1)); // context
            processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Ldind_Ref));
            processor.InsertBefore(firstInstruction, processor.Create(OpCodes.Call, forwardMethodRef)); // Call the pre-hook method

            return true;
        }
    }
}
