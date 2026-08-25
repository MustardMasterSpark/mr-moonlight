// Copyright © Magnetic Arcade. All Rights Reserved.

#if HAS_PACKAGE_SHADERGRAPH
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MA.Flora.CodeGen.Forwarding;
using UnityEditor;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;

namespace MA.Flora.Editor.ShaderGraph
{
    internal static class FloraShaderGraphGenerator
    {
        private const BindingFlags AllMemberBindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

        private static FieldInfo s_Generator_Mode_Field;
        private static FieldInfo s_Generator_Targets_Field;
        private static FieldInfo s_Generator_GraphData_Field;
        private static FieldInfo s_PragmaCollection_Items_Field;
        private static FieldInfo s_PropertyCollector_Properties_Field;
        private static FieldInfo s_IncludeCollection_Includes_Field;

        [InitializeOnLoadMethod]
        private static void InitializeIncludeHelpers()
        {
            s_Generator_Mode_Field = typeof(Generator).GetField("m_Mode", AllMemberBindingFlags);
            s_Generator_Targets_Field = typeof(Generator).GetField("m_Targets", AllMemberBindingFlags);
            s_Generator_GraphData_Field = typeof(Generator).GetField("m_GraphData", AllMemberBindingFlags);
            s_PragmaCollection_Items_Field = typeof(PragmaCollection).GetField("m_Items", AllMemberBindingFlags);
            s_PropertyCollector_Properties_Field = typeof(PropertyCollector).GetField("m_Properties", AllMemberBindingFlags);
            s_IncludeCollection_Includes_Field = typeof(IncludeCollection).GetField("includes", AllMemberBindingFlags);

            ShaderGraphEvents.PreGenerateShaderPass = OnPreGenerateShaderPass;
            ShaderGraphEvents.PreGenerateSubShader = OnPreGenerateSubShader;
            ShaderGraphEvents.PreGetFields = OnPreGetFields;
            ShaderGraphEvents.PreGetActiveBlocks = OnPreGetActiveBlocks;
        }

        private static readonly KeywordDescriptor DebugDisplayKeyword = new()
        {
            displayName = "Debug Display",
            referenceName = "DEBUG_DISPLAY",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
            stages = KeywordShaderStage.Fragment,
        };

        private static readonly HashSet<string> ValidSubTargets = new(StringComparer.InvariantCultureIgnoreCase)
        {
            "UniversalLitSubTarget",
            "UniversalUnlitSubTarget",
            "HDLitSubTarget",
            "HDUnlitSubTarget",
        };

        private static readonly HashSet<string> InvalidPassNames = new(StringComparer.InvariantCultureIgnoreCase)
        {
            "Meta",
        };

        private static readonly HashSet<string> InvalidShaderPassNames = new(StringComparer.InvariantCultureIgnoreCase)
        {
            "SHADERPASS_META",
        };

        private static readonly HashSet<string> SelectionPassNames = new(StringComparer.InvariantCultureIgnoreCase)
        {
            "SceneSelectionPass",
            "ScenePickingPass"
        };

        private static bool IsValidSubTarget(Target target)
        {
            var targetType = target.GetType();
            var activeSubTargetProperty = targetType.GetProperty("activeSubTarget", AllMemberBindingFlags);
            if (activeSubTargetProperty != null)
            {
                var activeSubTarget = activeSubTargetProperty.GetValue(target);
                if (activeSubTarget != null)
                {
                    var subTargetName = activeSubTarget.GetType().Name;
                    if (!ValidSubTargets.Contains(subTargetName))
                        return false;
                }
            }

            return true;
        }

        private static bool IsUniversalTarget(Target target)
        {
            return target.GetType().Namespace == "UnityEditor.Rendering.Universal.ShaderGraph";
        }

        private static object OnPreGenerateShaderPass(object generator, int passIndex, object boxedPass, object activeFields, object blockFieldDescriptors, object propertyCollector)
        {
            if (generator is Generator gen)
            {
                var pass = (PassDescriptor)boxedPass;
                GenerateShaderPass(gen, passIndex, ref pass, (ActiveFields)activeFields, (List<BlockFieldDescriptor>)(blockFieldDescriptors), (PropertyCollector)propertyCollector);
                return pass;
            }

            return null;
        }

        private static object OnPreGenerateSubShader(object generator, int targetIndex, object descriptor, object subShaderProperties)
        {
            if (generator is Generator gen)
            {
                var subShader = (SubShaderDescriptor)descriptor;
                GenerateSubShader(gen, targetIndex, ref subShader, (PropertyCollector)subShaderProperties);
                return subShader;
            }

            return null;
        }

        private static void OnPreGetFields(object subtarget, object context)
        {
        }

        private static void OnPreGetActiveBlocks(object subtarget, object context)
        {
        }

        private static void GenerateSubShader(Generator generator, int targetIndex, ref SubShaderDescriptor subShaderDescriptor, PropertyCollector subShaderProperties)
        {
            var mode = (GenerationMode)s_Generator_Mode_Field.GetValue(generator);
            if (mode != GenerationMode.ForReals)
                return; // Never alter VFX passes
        }

        private static void GenerateShaderPass(
            Generator generator,
            int targetIndex,
            ref PassDescriptor pass,
            ActiveFields activeFields,
            List<BlockFieldDescriptor> currentBlockDescriptors,
            PropertyCollector subShaderProperties)
        {
            var mode = (GenerationMode)s_Generator_Mode_Field.GetValue(generator);
            if (mode != GenerationMode.ForReals)
                return; // Never alter VFX passes

            if (s_Generator_Targets_Field.GetValue(generator) is not IList targets)
                return;

            if (targets[targetIndex] is not Target target)
                return;

            if (!IsValidSubTarget(target))
                return; // Skip invalid sub-targets

            if (InvalidPassNames.Contains(pass.displayName) ||
                InvalidShaderPassNames.Contains(pass.referenceName))
            {
                return;
            }

#if !FLORA_DISABLE_SHADER_GRAPH_INJECTION
            pass.includes = new IncludeCollection { pass.includes ?? new IncludeCollection() };
            pass.includes.Add(new IncludeCollection
            {
                { "Packages/com.ma.flora/ShaderLibrary/Instancing.hlsl", IncludeLocation.Pregraph, false },
            });
#endif
        }
    }
}
#endif
