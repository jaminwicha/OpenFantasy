using System.Text.Json;
using OpenFantasy.Core.Commands;

using var fixtures = JsonDocument.Parse(File.ReadAllText(args[0]));
int accepted = 0, rejected = 0;
void Check(byte[] bytes, bool receipt)
{
    try
    {
        byte[] canonical = receipt
            ? CommandReceiptCodecV1.Encode(CommandReceiptCodecV1.Decode(bytes))
            : CommandCodecV1.Encode(CommandCodecV1.Decode(bytes));
        if (!canonical.AsSpan().SequenceEqual(bytes)) throw new Exception("Accepted noncanonical bytes");
        accepted++;
    }
    catch (CommandFormatException) { rejected++; }
}
foreach (string field in new[] { "commandHex", "appliedReceiptHex", "rejectedReceiptHex" })
{
    byte[] golden = Convert.FromHexString(fixtures.RootElement.GetProperty(field).GetString()!);
    for (int index = 0; index < golden.Length; index++)
    for (int bit = 0; bit < 8; bit++)
    {
        byte[] mutation = (byte[])golden.Clone();
        mutation[index] ^= (byte)(1 << bit);
        Check(mutation, field != "commandHex");
    }
}
Random random = new(19873);
for (int i = 0; i < 10000; i++)
{
    byte[] bytes = new byte[random.Next(0, CommandCodecV1.MaximumEnvelopeByteLength + 32)];
    random.NextBytes(bytes);
    Check(bytes, i % 2 == 0);
}
Console.WriteLine(JsonSerializer.Serialize(new { acceptedCanonicalMutations = accepted, rejectedTypedFormatFailures = rejected, total = accepted + rejected, unhandledExceptions = 0 }));
