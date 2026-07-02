using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;
using System.Text;

using MarkdigHtmlRenderer = Markdig.Renderers.HtmlRenderer;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Converts assistant Markdown to Blazor markup without allowing URL-bearing HTML output.
    /// </summary>
    public static class AssistantMarkdownRenderer
    {
        private static readonly MarkdownPipeline kAssistantMarkdownPipeline = new MarkdownPipelineBuilder()
            .DisableHtml()
            .UseAdvancedExtensions()
            .Use(new PlainTextUrlMarkdownExtension())
            .Build();

        /// <summary>
        /// Converts assistant Markdown to Blazor markup with raw HTML and URL links disabled.
        /// </summary>
        public static MarkupString Render(string? markdown)
        {
            return new MarkupString(Markdown.ToHtml(markdown ?? "", kAssistantMarkdownPipeline));
        }

        private sealed class PlainTextUrlMarkdownExtension : IMarkdownExtension
        {
            /// <summary>
            /// Keeps the parser unchanged and only replaces URL renderers.
            /// </summary>
            public void Setup(MarkdownPipelineBuilder pipeline) { }

            /// <summary>
            /// Renders Markdown links and automatic URLs as escaped text instead of anchors or images.
            /// </summary>
            public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
            {
                if (renderer is MarkdigHtmlRenderer htmlRenderer)
                {
                    htmlRenderer.ObjectRenderers.RemoveAll(currentRenderer =>
                        currentRenderer is LinkInlineRenderer || currentRenderer is AutolinkInlineRenderer);
                    htmlRenderer.ObjectRenderers.Add(new PlainTextLinkInlineRenderer());
                    htmlRenderer.ObjectRenderers.Add(new PlainTextAutolinkInlineRenderer());
                }
            }
        }

        private sealed class PlainTextLinkInlineRenderer : HtmlObjectRenderer<LinkInline>
        {
            /// <summary>
            /// Writes links and images as escaped Markdown source text without emitting URL-bearing HTML.
            /// </summary>
            protected override void Write(MarkdigHtmlRenderer renderer, LinkInline link)
            {
                string url = link.GetDynamicUrl?.Invoke() ?? link.Url ?? "";
                if (link.IsAutoLink)
                {
                    renderer.WriteEscape(url);
                    return;
                }

                string imagePrefix = link.IsImage ? "!" : "";
                renderer.WriteEscape($"{imagePrefix}[{GetInlinePlainText(link)}]({url})");
            }
        }

        private sealed class PlainTextAutolinkInlineRenderer : HtmlObjectRenderer<AutolinkInline>
        {
            /// <summary>
            /// Writes angle-bracket autolinks as escaped source text.
            /// </summary>
            protected override void Write(MarkdigHtmlRenderer renderer, AutolinkInline link)
            {
                renderer.WriteEscape($"<{link.Url}>");
            }
        }

        private static string GetInlinePlainText(ContainerInline container)
        {
            StringBuilder text = new();
            for (Inline? inline = container.FirstChild; inline != null; inline = inline.NextSibling)
            {
                AppendInlinePlainText(text, inline);
            }
            return text.ToString();
        }

        private static void AppendInlinePlainText(StringBuilder text, Inline inline)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case LineBreakInline:
                    text.AppendLine();
                    break;
                case ContainerInline container:
                    text.Append(GetInlinePlainText(container));
                    break;
                default:
                    text.Append(inline.ToString());
                    break;
            }
        }
    }
}
