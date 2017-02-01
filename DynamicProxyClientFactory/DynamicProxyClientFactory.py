import clr
clr.AddReference('System.Core')
clr.AddReference('System.Configuration')
clr.AddReference('System.ServiceModel')
clr.AddReference('System.Xml')

from System import *
from System.CodeDom import *
from System.CodeDom.Compiler import *
from System.Globalization import *
from System.IO import *
from System.Linq import *
from System.Reflection import *
from System.ServiceModel import *
from System.ServiceModel.Description import *
from System.Threading.Tasks import *

def CreateDynamicProxyClientFactory(uri):
	mexClient = MetadataExchangeClient(uri, MetadataExchangeClientMode.HttpGet, ResolveMetadataReferences = True)
	metaDocs = mexClient.GetMetadata()
	importer = WsdlImporter(metaDocs)
	contracts = importer.ImportAllContracts()
	allEndpoints = importer.ImportAllEndpoints()
	generator = ServiceContractGenerator(importer.State[clr.GetClrType(CodeCompileUnit)])
	generator.Options = \
		ServiceContractGenerationOptions.TaskBasedAsynchronousMethod \
		| ServiceContractGenerationOptions.ChannelInterface \
		| ServiceContractGenerationOptions.ClientClass
	enumerator = contracts.GetEnumerator()
	while enumerator.MoveNext():
		contract = enumerator.Current
		generator.GenerateServiceContractType(contract)
	if generator.Errors.Count != 0:
		raise Exception("There were errors during code generation.")
	codeDomProvider = CodeDomProvider.CreateProvider("C#")
	compiled = codeDomProvider.CompileAssemblyFromDom(
		CompilerParameters(
			GenerateInMemory = True,
			TreatWarningsAsErrors = False,
			TempFiles = TempFileCollection(".", KeepFiles = False)),
		generator.TargetCompileUnit)
	if compiled.Errors.HasErrors:
		raise Exception("There were errors during code compilation")
	assembly = compiled.CompiledAssembly
	def createClientProxy(contractName):
		contract = Enumerable.First(contracts, lambda c: c.Name == contractName)
		endpoint = Enumerable.First(allEndpoints, lambda ep: ep.Contract == contract)
		clientProxyType = Enumerable.First(assembly.GetTypes(), lambda t: \
		 	t.IsClass \
		 	and t.GetInterface(contract.Name) != None \
		 	and t.GetInterface(clr.GetClrType(ICommunicationObject).Name) != None)
		return assembly.CreateInstance(
			typeName = clientProxyType.Name,
			args = Array[Object]((endpoint.Binding, endpoint.Address)),
			
			ignoreCase = False,
			bindingAttr = BindingFlags.Default,
			binder = None,
			culture = CultureInfo.CurrentCulture,
			activationAttributes = Array.CreateInstance(Object, 0))
	return createClientProxy

createClient = CreateDynamicProxyClientFactory(Uri("http://www.dneonline.com/calculator.asmx?wsdl"))
client = createClient("CalculatorSoap")
a = 5
b = 17

def onCanculationResult(t):
	Console.WriteLine("{0} + {1} = {2}", a, b, t.Result)

client.AddAsync(a, b).ContinueWith(
	Func[Task[int], type(None)](lambda t: onCanculationResult(t)), 
	continuationOptions = TaskContinuationOptions.OnlyOnRanToCompletion)

Console.ReadLine()
