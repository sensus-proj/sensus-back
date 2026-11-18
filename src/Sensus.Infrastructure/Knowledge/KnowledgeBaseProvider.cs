using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Knowledge;

/// <summary>
/// Proveedor de la base de conocimiento estática RAG para Sensi IA (FR-046-03, NFR-046-01).
/// Carga el archivo Markdown versionado, calcula su hash SHA-256 e impide respaldos silenciosos en producción.
/// </summary>
public sealed class KnowledgeBaseProvider : IKnowledgeBaseProvider
{
    private const string KnowledgeFileName = "sensi-app-guide.v1.0.0.md";
    private readonly KnowledgeBaseDocument _cachedDocument;

    public KnowledgeBaseProvider()
    {
        var content = LoadKnowledgeMarkdown();
        var hash = ComputeSha256(content);
        _cachedDocument = new KnowledgeBaseDocument("1.0.0", hash, content);
    }

    public KnowledgeBaseDocument GetDocument() => _cachedDocument;

    private static string LoadKnowledgeMarkdown()
    {
        // 1. Búsqueda en rutas relativas al directorio base de ejecución / publicación
        var searchPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Knowledge", KnowledgeFileName),
            Path.Combine(AppContext.BaseDirectory, KnowledgeFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Knowledge", KnowledgeFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Sensus.Infrastructure", "Knowledge", KnowledgeFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "src", "Sensus.Infrastructure", "Knowledge", KnowledgeFileName)
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
        }

        // 2. Búsqueda como EmbeddedResource en el ensamblado (garantizado en paquete AWS Lambda)
        var assembly = typeof(KnowledgeBaseProvider).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(KnowledgeFileName, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
        }

        // 3. Fallo estricto al arrancar salvo en entorno Testing
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            return "# Sensus Testing Knowledge Base\nEntorno de pruebas.";
        }

        throw new FileNotFoundException(
            $"El archivo de base de conocimiento Markdown '{KnowledgeFileName}' es obligatorio y no fue encontrado.");
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
