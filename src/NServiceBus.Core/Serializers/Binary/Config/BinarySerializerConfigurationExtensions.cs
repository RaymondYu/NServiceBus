﻿namespace NServiceBus
{
    using Features;
    using Settings;

    public static class BinarySerializerConfigurationExtensions
    {
        /// <summary>
        /// Enables the binary message serializer
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static SerializationSettings Binary(this SerializationSettings settings)
        {
            Feature.Enable<BinarySerialization>();

            return settings;
        }
    }
}