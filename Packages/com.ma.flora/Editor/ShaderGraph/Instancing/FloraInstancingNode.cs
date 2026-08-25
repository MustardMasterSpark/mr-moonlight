// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace MA.Flora.Editor.ShaderGraph
{
    [Obsolete]
    [FormerName("MA.Flora.Editor.ShaderGraph.SetupFloraInstancingDataNode")]
    internal sealed class FloraInstancingNode : AbstractMaterialNode
        , IGeneratesBodyCode
        , IGeneratesFunction
        , IMayRequirePosition
    {
        public const string Category = "Flora";
        public const string Name     = "Flora Instancing";

        public const string InputSlotName  = "Vertex Position";
        public const string OutputSlotName = "PositionOS";

        public const int InputSlotId  = 0;
        public const int OutputSlotId = 1;

        public FloraInstancingNode()
        {
            name = Name;
            precision = Precision.Single;
            synonyms = new [] { "flora", "instancing", "instanced", "procedural" };
            UpdateNodeAfterDeserialization();
        }

        public override bool hasPreview => false;

        public override void UpdateNodeAfterDeserialization()
        {
            AddSlot(new PositionMaterialSlot(InputSlotId, InputSlotName, InputSlotName, CoordinateSpace.Object, ShaderStageCapability.Vertex));
            AddSlot(new Vector3MaterialSlot(OutputSlotId, OutputSlotName, OutputSlotName, SlotType.Output, Vector3.zero, ShaderStageCapability.Vertex));
            RemoveSlotsNameNotMatching(new[] { InputSlotId, OutputSlotId });
        }

        public void GenerateNodeCode(ShaderStringBuilder sb, GenerationMode generationMode)
        {
#if FLORA_DISABLE_SHADER_GRAPH_INJECTION
            sb.AppendLine("#pragma instancing_options procedural:SetupFloraInstancingData");
#endif
            var outputName = GetVariableNameForSlot(OutputSlotId);
            var inputValue = GetSlotValue(InputSlotId, generationMode);
            sb.AppendLine("float3 {0} = {1};", outputName, inputValue);
        }

        public void GenerateNodeFunction(FunctionRegistry registry, GenerationMode generationMode)
        {
            registry.RequiresIncludes(new IncludeCollection
            {
                { "Packages/com.ma.flora/ShaderLibrary/Instancing.hlsl", IncludeLocation.Pregraph, true },
            });
        }

        public override void CollectShaderProperties(PropertyCollector propertyCollector, GenerationMode generationMode)
        {
            propertyCollector.AddShaderProperty(new FloraVersionProperty());
            base.CollectShaderProperties(propertyCollector, generationMode);
        }

        public NeededCoordinateSpace RequiresPosition(ShaderStageCapability stageCapability = ShaderStageCapability.All)
        {
            return stageCapability is ShaderStageCapability.All or ShaderStageCapability.Vertex
                ? NeededCoordinateSpace.Object
                : NeededCoordinateSpace.None;
        }
    }
}
