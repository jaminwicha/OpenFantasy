using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

string assemblyPath = Path.GetFullPath(args[0]);
string pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
string sourcePath = Path.GetFullPath(args[1]);
using var peStream = File.OpenRead(assemblyPath);
using var pe = new PEReader(peStream);
var debug = pe.ReadDebugDirectory().Single(entry => entry.Type == DebugDirectoryEntryType.CodeView);
var codeView = pe.ReadCodeViewDebugDirectoryData(debug);
using var pdbStream = File.OpenRead(pdbPath);
using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
var reader = provider.GetMetadataReader();
byte[] id = reader.DebugMetadataHeader!.Id.ToArray();
if (codeView.Guid != new Guid(id.AsSpan(0, 16)) || debug.Stamp != BitConverter.ToUInt32(id, 16))
    throw new InvalidDataException("The PDB does not match the executed assembly's debug identity.");
var document = reader.Documents.Select(reader.GetDocument)
    .Single(value => Path.GetFullPath(reader.GetString(value.Name)) == sourcePath);
Guid algorithm = reader.GetGuid(document.HashAlgorithm);
if (algorithm != new Guid("8829d00f-11b8-4213-878b-770e8597ac16"))
    throw new InvalidDataException("Expected the compiler's SHA256 source checksum.");
byte[] expected = reader.GetBlobBytes(document.Hash);
byte[] actual = SHA256.HashData(File.ReadAllBytes(sourcePath));
if (!actual.SequenceEqual(expected)) throw new InvalidDataException("Source differs from the compiled document.");
string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
Console.WriteLine(JsonSerializer.Serialize(new {
    assembly = assemblyPath, assemblySha256 = Hash(assemblyPath), pdb = pdbPath,
    pdbSha256 = Hash(pdbPath), source = sourcePath, sourceSha256 = Hash(sourcePath),
    sourceMatchesCompiledChecksum = true, pdbMatchesAssemblyDebugIdentity = true,
    codeViewGuid = codeView.Guid, debugStamp = debug.Stamp
}, new JsonSerializerOptions { WriteIndented = true }));
