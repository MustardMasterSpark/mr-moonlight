// Copyright © Magnetic Arcade. All Rights Reserved.

namespace MA.Flora.CodeGen.Forwarding
{
    /// Avoiding circular references between generated code and Unity.ShaderGraph.Editor.
    public static class ShaderGraphEvents
    {
        public delegate object PreGenerateShaderPassDelegate(object generator, int passIndex, object passDescriptor, object activeFields, object blockFieldDescriptors, object propertyCollector);

        public static PreGenerateShaderPassDelegate PreGenerateShaderPass;

        internal static object ForwardPreGenerateShaderPass(object generator, int passIndex, object passDescriptor, object activeFields, object blockFieldDescriptors, object propertyCollector)
        {
            return PreGenerateShaderPass?.Invoke(generator, passIndex, passDescriptor, activeFields, blockFieldDescriptors, propertyCollector);
        }

        public delegate object PreGenerateSubShaderDelegate(object generator, int targetIndex, object descriptor, object subShaderProperties);

        public static PreGenerateSubShaderDelegate PreGenerateSubShader;

        internal static object ForwardPreGenerateSubShader(object generator, int targetIndex, object descriptor, object subShaderProperties)
        {
            return PreGenerateSubShader?.Invoke(generator, targetIndex, descriptor, subShaderProperties);
        }

        public delegate void PreGetActiveBlocksDelegate(object subTarget, object context);

        public static PreGetActiveBlocksDelegate PreGetActiveBlocks;

        internal static void ForwardPreGetActiveBlocks(object subTarget, object context)
        {
            PreGetActiveBlocks?.Invoke(subTarget, context);
        }

        public delegate void PreGetFieldsDelegate(object subTarget, object context);

        public static PreGetFieldsDelegate PreGetFields;

        internal static void ForwardPreGetFields(object subTarget, object context)
        {
            PreGetFields?.Invoke(subTarget, context);
        }
    }
}
