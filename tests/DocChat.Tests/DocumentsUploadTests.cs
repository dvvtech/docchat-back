using System.Net.Http.Json;
using System.Text.Json;
using DocChat.Api.Models.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DocChat.Tests
{
    public sealed class DocChatApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProxySettings:Enabled"] = "false"
                });
            });
        }
    }

    public sealed class DocumentsUploadTests : IClassFixture<DocChatApiFactory>
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly DocChatApiFactory _factory;

        public DocumentsUploadTests(DocChatApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Upload_FromDataFolder_IngestsDocumentIntoQdrant()
        {
            var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
            var filePath = Path.Combine(dataDirectory, "rag_test_document_7_pages.txt");

            Assert.True(File.Exists(filePath), $"Test document not found: {filePath}");

            var documentId = $"integration-test-{Guid.NewGuid():N}";
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

            DocumentUploadResponse? response;

            try
            {
                await using var fileStream = File.OpenRead(filePath);
                using var request = new MultipartFormDataContent();
                request.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));
                request.Add(new StringContent(documentId), "documentId");

                var httpResponse = await client.PostAsync("documents/upload", request);

                Assert.True(httpResponse.IsSuccessStatusCode, await httpResponse.Content.ReadAsStringAsync());

                response = await httpResponse.Content.ReadFromJsonAsync<DocumentUploadResponse>(JsonOptions);
            }
            finally
            {
                try
                {
                    await client.DeleteAsync($"documents/{documentId}");
                }
                catch
                {
                }
            }

            Assert.NotNull(response);
            Assert.Equal(documentId, response.DocumentId);
            Assert.Equal(Path.GetFileName(filePath), response.FileName);
            Assert.True(response.TextCharacterCount > 0);
            Assert.True(response.ChunkCount > 0);
            Assert.Equal(response.ChunkCount, response.Chunks.Count);
        }
    }
}
