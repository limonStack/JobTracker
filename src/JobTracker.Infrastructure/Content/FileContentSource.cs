using System.Security.Cryptography;
using System.Text;
using JobTracker.Application.Content;
using Microsoft.Extensions.Options;

namespace JobTracker.Infrastructure.Content;

/// <summary>
/// Тексты из HTML-файлов в папке приложения. Каждый файл — набор разделов; файлы дополняют друг друга
/// (публичные разделы + личные, которых нет в git). Версия — SHA-256 содержимого и версии разбора.
/// </summary>
internal sealed class FileContentSource(IOptions<ContentOptions> options) : IContentSource
{
    public async Task<ContentPackage?> LoadAsync(CancellationToken ct)
    {
        var o = options.Value;
        var root = Path.Combine(AppContext.BaseDirectory, o.Directory);
        var files = o.Files.Select(f => Path.Combine(root, f)).Where(File.Exists).ToList();
        if (files.Count == 0)
            return null;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(AnswersHtmlParser.Version));
        var sections = new List<Domain.Knowledge.Section>();
        foreach (var file in files)
        {
            var bytes = await File.ReadAllBytesAsync(file, ct);
            hash.AppendData(bytes);
            sections.AddRange(AnswersHtmlParser.Parse(Encoding.UTF8.GetString(bytes)));
        }

        var duplicate = sections.GroupBy(s => s.Number).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Раздел {duplicate.Key} есть в нескольких файлах ({string.Join(", ", files)}).");

        return new ContentPackage(
            Convert.ToHexStringLower(hash.GetHashAndReset()),
            sections.OrderBy(s => s.Number).ToList(),
            files.Select(f => Path.GetRelativePath(root, f)).ToList());
    }
}
