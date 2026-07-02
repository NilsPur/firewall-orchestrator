using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AiToolExecutionServiceTest
    {
        private string helpDirectory = "";

        [SetUp]
        public void SetUp()
        {
            helpDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ai-help-test-" + Guid.NewGuid());
            Directory.CreateDirectory(helpDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(helpDirectory))
            {
                Directory.Delete(helpDirectory, true);
            }
        }

        [Test]
        public void SearchHelpDocumentation_ReturnsMatchingHelpExcerpt()
        {
            File.WriteAllText(Path.Combine(helpDirectory, "HelpAssistant.cshtml"),
                "<h1>Assistant</h1><p>The AI assistant answers questions about firewall rules and reports.</p>");
            File.WriteAllText(Path.Combine(helpDirectory, "HelpSettings.cshtml"),
                "<h1>Settings</h1><p>Configure tenants and imports.</p>");

            string result = AiToolExecutionService.SearchHelpDocumentation("firewall assistant", helpDirectory);

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Assistant"));
                Assert.That(result, Does.Contain("firewall rules"));
                Assert.That(result, Does.Not.Contain("Settings:"));
            });
        }

        [Test]
        public void SearchHelpDocumentation_ResolvesLocalizedHelpText()
        {
            string textsFile = Path.Combine(helpDirectory, "fworch-texts.sql");
            File.WriteAllText(Path.Combine(helpDirectory, "HelpAssistant.cshtml"),
                """
                <h3>@(userConfig.GetText("assistant"))</h3>
                @(Html.Raw(userConfig.GetText("H9200")))
                """);
            File.WriteAllText(textsFile,
                """
                INSERT INTO txt VALUES ('assistant', 'English', 'Assistant');
                INSERT INTO txt VALUES ('assistant', 'German', 'Assistent');
                INSERT INTO txt VALUES ('H9200', 'German', 'Der Assistent verwaltet Chatsitzungen.');
                INSERT INTO txt VALUES ('H9200', 'English', 'The assistant sends messages to the configured model and shows tool calls.');
                """);

            string result = AiToolExecutionService.SearchHelpDocumentation("configured model", helpDirectory, textsFile);

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("configured model"));
                Assert.That(result, Does.Contain("tool calls"));
                Assert.That(result, Does.Not.Contain("userConfig.GetText"));
                Assert.That(result, Does.Not.Contain("H9200"));
            });
        }

        [Test]
        public void SearchHelpDocumentation_ReturnsNoMatchMessage()
        {
            File.WriteAllText(Path.Combine(helpDirectory, "HelpSettings.cshtml"),
                "<h1>Settings</h1><p>Configure tenants and imports.</p>");

            string result = AiToolExecutionService.SearchHelpDocumentation("recertification", helpDirectory);

            Assert.That(result, Is.EqualTo("No FWO help page matched 'recertification'."));
        }

        [Test]
        public void SearchHelpDocumentation_ReturnsMissingHelpMessage()
        {
            string missingDirectory = Path.Combine(helpDirectory, "missing");

            string result = AiToolExecutionService.SearchHelpDocumentation("assistant", missingDirectory);

            Assert.That(result, Is.EqualTo("FWO help files are not available on this middleware host."));
        }
    }
}
