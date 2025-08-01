using FWO.Api.Client;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Data;
using FWO.Report.Filter;
using System.Text;
using System.Reflection;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using PuppeteerSharp.BrowserData;
using HtmlAgilityPack;

namespace FWO.Report
{
    public enum RsbTab
    {
        all = 10,
        report = 20,
        rule = 30,

        usedObj = 40,
        unusedObj = 50
    }

    public enum ObjCategory
    {
        all = 0,
        nobj = 1,
        nsrv = 2,
        user = 3
    }

    public struct ObjCatString
    {
        public const string NwObj = "nwobj";
        public const string Svc = "svc";
        public const string User = "user";
    }

    public enum OutputLocation
    {
        export,
        report,
        certification
    }

    public abstract class ReportBase
    {
        protected StringBuilder HtmlTemplate = new($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8""/>
      <title>##Title##</title>
         <style>  
             table {{
                font-family: arial, sans-serif;
                font-size: 10px;
                border-collapse: collapse; 
                width: 100 %;
              }}

              td {{
                border: 1px solid #000000;
                text-align: left;
                padding: 3px;
              }}

              th {{
                border: 1px solid #000000;
                text-align: left;
                padding: 3px;
                background-color: #dddddd;
              }}
         </style>
    </head>
    <body>
        <h2>##Title##</h2>
        <p>##Date-of-Config##: ##GeneratedFor## (UTC)</p>
        <p>##GeneratedOn##: ##Date## (UTC)</p>
        <p>##OwnerFilters##</p>
        <p>##OtherFilters##</p>
        <p>##Filter##</p>
        <hr>
        ##ToC##
        <hr>
        ##Body##
    </body>
</html>");

        public readonly DynGraphqlQuery Query;
        protected UserConfig userConfig;
        public ReportType ReportType { get; set; }
        public ReportData ReportData { get; set; } = new();
        public int CustomWidth { get; set; } = 0;
        public int CustomHeight { get; set; } = 0;

        protected string htmlExport = "";

        private string TocHTMLTemplate = "<div id=\"toc_container\"><h2>##ToCHeader##</h2><ul class=\"toc_list\">##ToCList##</ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style>";

        public bool GotObjectsInReport { get; protected set; } = false;


        protected ReportBase(DynGraphqlQuery query, UserConfig UserConfig, ReportType reportType)
        {
            Query = query;
            userConfig = UserConfig;
            ReportType = reportType;
        }

        public abstract Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct);

        public virtual async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            await callback(ReportData);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
            return true;
        }

        public virtual Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            throw new NotImplementedException();
        }

        public virtual bool NoRuleFound()
        {
            return true;
        }

        public abstract string ExportToCsv();

        public abstract string ExportToJson();

        public abstract string ExportToHtml();

        public abstract string SetDescription();

        public static ReportBase ConstructReport(ReportTemplate reportFilter, UserConfig userConfig)
        {
            DynGraphqlQuery query = Compiler.Compile(reportFilter);
            ReportType repType = (ReportType)reportFilter.ReportParams.ReportType;
            return repType switch
            {
                ReportType.Statistics => new ReportStatistics(query, userConfig, repType),
                ReportType.Rules => new ReportRules(query, userConfig, repType),
                ReportType.ResolvedRules => new ReportRules(query, userConfig, repType),
                ReportType.ResolvedRulesTech => new ReportRules(query, userConfig, repType),
                ReportType.Changes => new ReportChanges(query, userConfig, repType, reportFilter.ReportParams.TimeFilter),
                ReportType.ResolvedChanges => new ReportChanges(query, userConfig, repType, reportFilter.ReportParams.TimeFilter),
                ReportType.ResolvedChangesTech => new ReportChanges(query, userConfig, repType, reportFilter.ReportParams.TimeFilter),
                ReportType.NatRules => new ReportNatRules(query, userConfig, repType),
                ReportType.Recertification => new ReportRules(query, userConfig, repType),
                ReportType.UnusedRules => new ReportRules(query, userConfig, repType),
                ReportType.Connections => new ReportConnections(query, userConfig, repType),
                ReportType.AppRules => new ReportAppRules(query, userConfig, repType, reportFilter.ReportParams.ModellingFilter),
                ReportType.VarianceAnalysis => new ReportVariances(query, userConfig, repType),
                _ => throw new NotSupportedException("Report Type is not supported."),
            };
        }

        public static string GetLinkAddress(OutputLocation location, string reportId, string type, int chapterNumber, long id, ReportType reportType)
        {
            string page = location == OutputLocation.report ? PageName.ReportGeneration : PageName.Certification;
            string link;
            if(reportType.IsChangeReport())
            {
                link = location == OutputLocation.export ? $"#" : $"{page}#goto-all-{reportId}-";
            }
            else
            {
                link = location == OutputLocation.export ? $"#" : $"{page}#goto-report-{reportId}-";
            }
            return $"{link}{type}{chapterNumber}x{id}";
        }

        public static string ConstructLink(string symbol, string name, string style, string linkAddress)
        {
            return $"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{linkAddress}\" target=\"_top\" style=\"{style}\">{name}</a>";
        }

        protected string GenerateHtmlFrameBase(string title, string filter, DateTime date, StringBuilder htmlReport, string? deviceFilter = null, string? ownerFilter = null, TimeFilter? timeFilter = null)
        {
            if(string.IsNullOrEmpty(htmlExport))
            {
                HtmlTemplate = HtmlTemplate.Replace("##Title##", title);
                if(filter != "")
                {
                    HtmlTemplate = HtmlTemplate.Replace("##Filter##", userConfig.GetText("filter") + ": " + filter);
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("<p>##Filter##</p>", "");
                }
                HtmlTemplate = HtmlTemplate.Replace("##GeneratedOn##", userConfig.GetText("generated_on"));
                HtmlTemplate = HtmlTemplate.Replace("##Date##", date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK"));
                if(ReportType.IsChangeReport())
                {
                    (string startTime, string stopTime) = DynGraphqlQuery.ResolveTimeRange(timeFilter!);
                    string timeRange = $"{userConfig.GetText("change_time")}: " +
                        $"{userConfig.GetText("from")}: {ToUtcString(startTime)}, " +
                        $"{userConfig.GetText("until")}: {ToUtcString(stopTime)}";
                    HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##: ##GeneratedFor##", timeRange);
                }
                else if(ReportType.IsRuleReport() || ReportType == ReportType.Statistics)
                {
                    HtmlTemplate = HtmlTemplate.Replace("##Date-of-Config##", userConfig.GetText("date_of_config"));
                    HtmlTemplate = HtmlTemplate.Replace("##GeneratedFor##", ToUtcString(Query.ReportTimeString));
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("<p>##Date-of-Config##: ##GeneratedFor## (UTC)</p>", "");
                }

                if(ownerFilter != null)
                {
                    HtmlTemplate = HtmlTemplate.Replace("##OwnerFilters##", userConfig.GetText("owners") + ": " + ownerFilter);
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("<p>##OwnerFilters##</p>", "");
                }

                if(deviceFilter != null)
                {
                    HtmlTemplate = HtmlTemplate.Replace("##OtherFilters##", userConfig.GetText("devices") + ": " + deviceFilter);
                }
                else
                {
                    HtmlTemplate = HtmlTemplate.Replace("<p>##OtherFilters##</p>", "");
                }

                string htmlToC = BuildHTMLToC(htmlReport.ToString());

                HtmlTemplate = HtmlTemplate.Replace("##ToC##", htmlToC);
                HtmlTemplate = HtmlTemplate.Replace("##Body##", htmlReport.ToString());
                htmlExport = HtmlTemplate.ToString();
            }
            return htmlExport;
        }

        public static string ToUtcString(string? timestring)
        {
            try
            {
                return timestring != null ? DateTime.Parse(timestring).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK") : "";
            }
            catch(Exception)
            {
                return timestring ?? "";
            }
        }

        private async Task<string?> CreatePDFViaPuppeteer(string html, PaperFormat format)
        {
            OperatingSystem? os = Environment.OSVersion;

            string path = "";
            Platform platform = Platform.Unknown;
            const SupportedBrowser wantedBrowser = SupportedBrowser.Chrome;

            switch(os.Platform)
            {
                case PlatformID.Win32NT:
                    platform = Platform.Win32;
                    break;
                case PlatformID.Unix:
                    path = GlobalConst.ChromeBinPathLinux;
                    platform = Platform.Linux;
                    break;
                default:
                    break;
            }

            BrowserFetcher browserFetcher = new(new BrowserFetcherOptions() { Platform = platform, Browser = wantedBrowser, Path = path });

            IEnumerable<InstalledBrowser>? allInstalledBrowsers = browserFetcher.GetInstalledBrowsers().Where(_ => _.Browser == wantedBrowser);

            if(!allInstalledBrowsers.Any())
            {
                if(os.Platform == PlatformID.Win32NT)
                {
                    Log.WriteInfo("Browser", $"Browser not found for Windows! Trying to download...");
                    await browserFetcher.DownloadAsync();
                    allInstalledBrowsers = browserFetcher.GetInstalledBrowsers().Where(_ => _.Browser == wantedBrowser);
                }
                else
                {
                    throw new EnvironmentException($"Found no installed {wantedBrowser} instances!");
                } 
            }

            string? newestBuildId = allInstalledBrowsers.Max(_ => _.BuildId);

            if(string.IsNullOrWhiteSpace(newestBuildId))
            {
                throw new EnvironmentException($"Invalid build ID!");
            }

            InstalledBrowser? latestInstalledBrowser = allInstalledBrowsers.Single(_ => _.BuildId == newestBuildId) ??
                throw new EnvironmentException($"Found no installed {wantedBrowser} instances with a valid build ID!");

            Log.WriteInfo("Test Log", $"Selecting latest installed {wantedBrowser}({latestInstalledBrowser.BuildId}) at: {latestInstalledBrowser.GetExecutablePath()}");

            IBrowser? browser;

            try
            {
                browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    ExecutablePath = latestInstalledBrowser.GetExecutablePath(),
                    Headless = true,
                });
            }
            catch(Exception)
            {
                Log.WriteAlert("Test Log", $"Couldn't start {wantedBrowser} instance!");
                throw new EnvironmentException($"Couldn't start {wantedBrowser} instance!");
            }            

            try
            {
                using IPage page = await browser.NewPageAsync();
                await page.SetContentAsync(html);

                PuppeteerSharp.Media.PaperFormat? pupformat = GetPuppeteerPaperFormat(format) ?? throw new KeyNotFoundException();

                PdfOptions pdfOptions = new() { Outline = true, DisplayHeaderFooter = false, Landscape = true, PrintBackground = true, Format = pupformat, MarginOptions = new MarginOptions { Top = "1cm", Bottom = "1cm", Left = "1cm", Right = "1cm" } };
                byte[]? pdfData = await page.PdfDataAsync(pdfOptions);

                return Convert.ToBase64String(pdfData);
            }
            catch(Exception)
            {
                throw new NotSupportedException("This paper kind is currently not supported. Please choose another one or \"Custom\" for a custom size.");
            }
            finally
            {
                await browser.CloseAsync();
                browser.Dispose();
            }
        }

        public static List<ToCHeader> CreateTOCContent(string html)
        {
            HtmlDocument doc = new();
            doc.LoadHtml(html);

            List<HtmlNode>? headings = [.. doc.DocumentNode.Descendants().Where(n => n.Name.StartsWith('h') && n.Name.Length == 2 && n.Name != "hr")];
            List<ToCHeader> tocs = [];

            int i = 0;

            foreach(HtmlNode heading in headings)
            {
                string headText = heading.InnerText.Trim();

                if(heading.Name == "h4" && tocs.Count > 0)
                {
                    tocs[i - 1].Items.Add(new ToCItem(headText, heading.Id));
                }
                else if(heading.Name == "h5" && tocs.Count > 0 && tocs[i - 1].Items.Count > 0)
                {
                    tocs[i - 1].Items[^1].SubItems.Add(new ToCItem(headText, heading.Id));
                }
                else if(heading.Name == "h6" && tocs.Count > 0 && tocs[i - 1].Items.Count > 0 && tocs[i - 1].Items[^1].SubItems.Count > 0)
                {
                    tocs[i - 1].Items[^1].SubItems[^1].SubItems.Add(new ToCItem(headText, heading.Id));
                }
                else
                {
                    tocs.Add(new(headText, heading.Id));
                    i++;
                }
            }
            return tocs;
        }

        public string BuildHTMLToC(string html)
        {
            bool tocTemplateValid = IsValidHTML(TocHTMLTemplate);

            if(!tocTemplateValid)
            {
                throw new ArgumentException(userConfig.GetText("E9302"));
            }

            List<ToCHeader>? tocHeaders = CreateTOCContent(html);

            TocHTMLTemplate = TocHTMLTemplate.Replace("##ToCHeader##", userConfig.GetText("tableofcontent"));

            StringBuilder sb = new();
            foreach(ToCHeader toCHeader in tocHeaders)
            {
                AppendHeader(sb, toCHeader);
            }

            TocHTMLTemplate = TocHTMLTemplate.Replace("##ToCList##", sb.ToString());
            bool tocValidHTML = IsValidHTML(TocHTMLTemplate);
            if(!tocValidHTML)
            {
                throw new ArgumentException(userConfig.GetText("E9302"));
            }

            return TocHTMLTemplate;
        }

        private static void AppendHeader(StringBuilder sb, ToCHeader toCHeader)
        {
            sb.AppendLine($"<li><a href=\"#{toCHeader.Id}\">{toCHeader.Title}</a></li>");

            if(toCHeader.Items.Count > 0)
            {
                sb.AppendLine("<ul>");

                foreach(ToCItem tocItem in toCHeader.Items)
                {
                    AppendItem(sb, tocItem);
                }
                sb.AppendLine("</ul>");
            }
        }

        private static void AppendItem(StringBuilder sb, ToCItem tocItem)
        {
            sb.AppendLine($"<li class=\"subli\"><a href=\"#{tocItem.Id}\">{tocItem.Title}</a></li>");
            if(tocItem.SubItems.Count > 0)
            {
                sb.AppendLine("<ul>");
                foreach(ToCItem subItem in tocItem.SubItems)
                {
                    AppendSubItem(sb, subItem);
                }
                sb.AppendLine("</ul>");
            }
        }

        private static void AppendSubItem(StringBuilder sb, ToCItem subItem)
        {
            sb.AppendLine($"<li class=\"subli\"><a href=\"#{subItem.Id}\">{subItem.Title}</a></li>");
            if(subItem.SubItems.Count > 0)
            {
                sb.AppendLine("<ul>");
                foreach(ToCItem subsubItem in subItem.SubItems)
                {
                    sb.AppendLine($"<li class=\"subli\"><a href=\"#{subsubItem.Id}\">{subsubItem.Title}</a></li>");
                }
                sb.AppendLine("</ul>");
            }
        }

        public static bool IsValidHTML(string html)
        {
            try
            {
                HtmlDocument? doc = new();
                doc.LoadHtml(html);
                return !doc.ParseErrors.Any();
            }
            catch(Exception)
            {
                return false;
            }

        }

        public PuppeteerSharp.Media.PaperFormat? GetPuppeteerPaperFormat(PaperFormat format)
        {
            if(format == PaperFormat.Custom)
                return new PuppeteerSharp.Media.PaperFormat(CustomWidth, CustomHeight);

            PropertyInfo[] propertyInfos = typeof(PuppeteerSharp.Media.PaperFormat).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);

            PropertyInfo? prop = propertyInfos.SingleOrDefault(_ => _.Name == format.ToString());

            if(prop == null)
                return default;

            PuppeteerSharp.Media.PaperFormat? propFormat = (PuppeteerSharp.Media.PaperFormat?)prop.GetValue(null);

            if(propFormat is null)
                return default;

            return propFormat;
        }

        public virtual async Task<string?> ToPdf(string html, PaperFormat format)
        {
            return await CreatePDFViaPuppeteer(html, format);
        }

        public virtual async Task<string?> ToPdf(string html)
        {
            return await CreatePDFViaPuppeteer(html, PaperFormat.A4);
        }

        public virtual async Task<string?> ToPdf(PaperFormat format)
        {
            return await CreatePDFViaPuppeteer(htmlExport, format);
        }

        public static string GetIconClass(ObjCategory? objCategory, string? objType)
        {
            return objType switch
            {
                ObjectType.Group when objCategory == ObjCategory.user => Icons.UserGroup,
                ObjectType.Group => Icons.ObjGroup,
                ObjectType.Host => Icons.Host,
                ObjectType.Network => Icons.Network,
                ObjectType.IPRange => Icons.Range,
                _ => objCategory switch
                {
                    ObjCategory.nobj => Icons.NwObject,
                    ObjCategory.nsrv => Icons.Service,
                    ObjCategory.user => Icons.User,
                    _ => "",
                },
            };
        }
    }
}
