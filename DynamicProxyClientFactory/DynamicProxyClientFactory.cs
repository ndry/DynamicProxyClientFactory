using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace DynamicProxyClientFactory
{
    public static class DynamicProxyClientFactory
    {
        public static Func<string, dynamic> Create(Uri uri)
        {
            var mexClient = new MetadataExchangeClient(uri, MetadataExchangeClientMode.HttpGet)
            {
                ResolveMetadataReferences = true
            };
            var metaDocs = mexClient.GetMetadata();

            var importer = new WsdlImporter(metaDocs);

            var contracts = importer.ImportAllContracts();
            var allEndpoints = importer.ImportAllEndpoints();

            var generator = new ServiceContractGenerator(
                (CodeCompileUnit)importer.State[typeof(CodeCompileUnit)])
            {
                Options = ServiceContractGenerationOptions.TaskBasedAsynchronousMethod
                          | ServiceContractGenerationOptions.ChannelInterface
                          | ServiceContractGenerationOptions.ClientClass
            };

            foreach (var contract in contracts)
            {
                generator.GenerateServiceContractType(contract);
            }

            if (generator.Errors.Count != 0)
            {
                throw new Exception("There were errors during code generation.");
            }

            var codeDomProvider = CodeDomProvider.CreateProvider("C#");

#if DEBUG
            // Output source to file
            using (var textWriter = new IndentedTextWriter(new StreamWriter(@".\ProxyClient.g.cs")))
            {
                codeDomProvider.GenerateCodeFromCompileUnit(generator.TargetCompileUnit, textWriter, new CodeGeneratorOptions());
                textWriter.Flush();
            }
#endif

            var compiled = codeDomProvider.CompileAssemblyFromDom(
                options: new CompilerParameters
                {
                    GenerateInMemory = true,
                    TreatWarningsAsErrors = false,
                    TempFiles = new TempFileCollection(@".") { KeepFiles = false }
                },
                compilationUnits: generator.TargetCompileUnit);

            if (compiled.Errors.HasErrors)
            {
                throw new Exception("There were errors during code compilation");
            }

            var assembly = compiled.CompiledAssembly;

            return contractName =>
            {
                var contract = contracts.First(c => c.Name == contractName);
                var endpoint = allEndpoints.First(ep => ep.Contract == contract);

                var clientProxyType = assembly.GetTypes().First(
                    t => t.IsClass &&
                         t.GetInterface(contract.Name) != null &&
                         t.GetInterface(typeof(ICommunicationObject).Name) != null);

                return assembly.CreateInstance(
                    typeName: clientProxyType.Name,
                    args: new object[] { endpoint.Binding, endpoint.Address },

                    ignoreCase: false,
                    bindingAttr: BindingFlags.Default,
                    binder: null,
                    culture: CultureInfo.CurrentCulture,
                    activationAttributes: new object[] { });
            };
        }
    }
}