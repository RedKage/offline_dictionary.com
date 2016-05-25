using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using offline_dictionary.com_shared;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com_export_jsondump
{
    public class ExportJsonDump : IExporter
    {
        private readonly string _outputDirPath;
        private readonly GenericDictionary _genericDictionary;

        public static JsonSerializer Serializer => new JsonSerializer
        {
            Formatting = Formatting.Indented,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
            TypeNameHandling = TypeNameHandling.Objects,
            Converters = { new DeepDictionaryConverter() },
            ContractResolver = new DictionaryAsArrayResolver()
        };
        
        public ExportJsonDump(GenericDictionary genericDictionary, string outputDirPath)
        {
            _genericDictionary = genericDictionary;
            _outputDirPath = outputDirPath;
        }

        public async Task ExportAsync(IProgress<ExportingProgressInfo> progress)
        {
            string outJsonDumpFilePath = $@"{_outputDirPath}\{_genericDictionary.Name}-{_genericDictionary.Version}.json.gz";

            Task convert = new Task(() =>
            {
                using (FileStream jsonFileStream = new FileStream(outJsonDumpFilePath, FileMode.Create, FileAccess.Write))
                {
                    using (GZipStream zipStream = new GZipStream(jsonFileStream, CompressionLevel.Optimal, false))
                    {
                        using (TextWriter jsonStream = new StreamWriter(zipStream))
                        {
                            using (JsonTextWriter jsonWriter = new JsonTextWriter(jsonStream))
                            {
                                Serializer.Serialize(jsonWriter, _genericDictionary, typeof(GenericDictionary));
                            }
                        }
                    }
                }
            });
            convert.Start();
            await convert;
        }

        public class DictionaryAsArrayResolver : DefaultContractResolver
        {
            protected override JsonContract CreateContract(Type objectType)
            {
                if (objectType.GetInterfaces()
                    .Any(i => i == typeof(IDictionary) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))))
                {
                    return CreateArrayContract(objectType);
                }

                return base.CreateContract(objectType);
            }
        }

        public class DeepDictionaryConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return (typeof(IDictionary).IsAssignableFrom(objectType) ||
                        TypeImplementsGenericInterface(objectType, typeof(IDictionary<,>)));
            }

            private static bool TypeImplementsGenericInterface(Type concreteType, Type interfaceType)
            {
                return concreteType.GetInterfaces()
                       .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Type type = value.GetType();
                IEnumerable keys = (IEnumerable)type.GetProperty("Keys").GetValue(value, null);
                IEnumerable values = (IEnumerable)type.GetProperty("Values").GetValue(value, null);
                IEnumerator valueEnumerator = values.GetEnumerator();

                writer.WriteStartArray();
                foreach (object key in keys)
                {
                    valueEnumerator.MoveNext();

                    writer.WriteStartArray();
                    serializer.Serialize(writer, key);
                    serializer.Serialize(writer, valueEnumerator.Current);
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}