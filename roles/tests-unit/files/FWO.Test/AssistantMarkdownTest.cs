using FWO.Ui.Services;
using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AssistantMarkdownTest
    {
        [Test]
        public void RenderMarkdown_RendersMarkdownLinksAsRawText()
        {
            string html = RenderAssistantMarkdown("[x](https://example.com/path?q=1)");

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Not.Contain("<a"));
                Assert.That(html, Does.Not.Contain("href="));
                Assert.That(html, Does.Contain("[x](https://example.com/path?q=1)"));
            });
        }

        [Test]
        public void RenderMarkdown_RendersUnsafeMarkdownLinksAsRawText()
        {
            string html = RenderAssistantMarkdown("[open](javascript:alert(1))");

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Not.Contain("<a"));
                Assert.That(html, Does.Not.Contain("href="));
                Assert.That(html, Does.Contain("[open](javascript:alert(1))"));
            });
        }

        [Test]
        public void RenderMarkdown_RendersImagesAsRawText()
        {
            string html = RenderAssistantMarkdown("![x](javascript:alert(1))");

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Not.Contain("<img"));
                Assert.That(html, Does.Not.Contain("src="));
                Assert.That(html, Does.Contain("![x](javascript:alert(1))"));
            });
        }

        [Test]
        public void RenderMarkdown_RendersAutomaticUrlsAsRawText()
        {
            string html = RenderAssistantMarkdown("Go to https://example.com/path");

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Not.Contain("<a"));
                Assert.That(html, Does.Not.Contain("href="));
                Assert.That(html, Does.Contain("https://example.com/path"));
            });
        }

        [Test]
        public void RenderMarkdown_DisablesRawHtml()
        {
            string html = RenderAssistantMarkdown("<script>alert(1)</script>");

            Assert.That(html, Does.Not.Contain("<script>"));
        }

        [Test]
        public void RenderMarkdown_KeepsNonUrlMarkdownFormatting()
        {
            string html = RenderAssistantMarkdown("**important**");

            Assert.That(html, Does.Contain("<strong>important</strong>"));
        }

        [Test]
        public void AssistantSettings_TestConnectionIconUsesPlug()
        {
            Assert.That(Icons.TestConnection, Does.Contain("plug"));
        }

        [Test]
        public void AssistantSettings_OllamaDownloadHintTextIsAvailable()
        {
            SimulatedUserConfig userConfig = new();

            string message = string.Format(userConfig.GetText("ai_model_downloaded_add_provider"), "llama3.2");

            Assert.That(message, Does.Contain("model provider"));
        }

        private static string RenderAssistantMarkdown(string markdown)
        {
            return AssistantMarkdownRenderer.Render(markdown).ToString();
        }
    }
}
