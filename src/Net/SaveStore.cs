using System.Text.Json;
using OpenFantasy.Sim;

namespace OpenFantasy.Net;

/// <summary>
/// Server-side character persistence: one JSON file mapping account name →
/// CharacterSave. Written on disconnect and on a 30 s autosave cadence.
/// </summary>
public sealed class SaveStore
{
    private readonly string _path;
    private readonly Dictionary<string, CharacterSave> _saves;

    public SaveStore(string path)
    {
        _path = path;
        _saves = new Dictionary<string, CharacterSave>(StringComparer.Ordinal);
        if (File.Exists(path))
        {
            try
            {
                _saves = JsonSerializer.Deserialize<Dictionary<string, CharacterSave>>(
                             File.ReadAllText(path), GameData.JsonOptions) ?? _saves;
            }
            catch (JsonException)
            {
                // Corrupt save file: keep it on disk for forensics, start fresh in memory.
                File.Copy(path, path + ".corrupt", overwrite: true);
            }
        }
    }

    public CharacterSave? Get(string account) => _saves.GetValueOrDefault(account);

    public void Put(string account, CharacterSave save) => _saves[account] = save;

    public void Flush()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_saves, GameData.JsonOptions));
        File.Move(tmp, _path, overwrite: true);
    }
}
