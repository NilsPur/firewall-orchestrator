﻿using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Logging;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;


namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ExportTest
    {
        static readonly NetworkObject TestIp1 = new() { Id = 1, Name = "TestIp1", IP = "1.2.3.4/32", IpEnd = "1.2.3.4/32", Type = new NetworkObjectType() { Name = ObjectType.Network } };
        static readonly NetworkObject TestIp2 = new() { Id = 2, Name = "TestIp2", IP = "127.0.0.1/32", IpEnd = "127.0.0.1/32", Type = new NetworkObjectType() { Name = ObjectType.Network } };
        static readonly NetworkObject TestIpRange = new() { Id = 3, Name = "TestIpRange", IP = "1.2.3.4/32", IpEnd = "1.2.3.5/32", Type = new NetworkObjectType() { Name = ObjectType.IPRange } };
        static readonly NetworkObject TestIpNew = new() { Id = 4, Name = "TestIpNew", IP = "10.0.6.0/32", IpEnd = "10.0.6.255/32", Type = new NetworkObjectType() { Name = ObjectType.Network } };
        static readonly NetworkObject TestIp1Changed = new() { Id = 5, Name = "TestIp1Changed", IP = "2.3.4.5/32", IpEnd = "2.3.4.5/32", Type = new NetworkObjectType() { Name = ObjectType.Host } };

        static readonly NetworkService TestService1 = new() { Id = 1, DestinationPort = 443, DestinationPortEnd = 443, Name = "TestService1", Protocol = new NetworkProtocol { Id = 6, Name = "TCP" } };
        static readonly NetworkService TestService2 = new() { Id = 2, DestinationPort = 6666, DestinationPortEnd = 7777, Name = "TestService2", Protocol = new NetworkProtocol { Id = 17, Name = "UDP" } };

        static readonly NetworkUser TestUser1 = new() { Id = 1, Name = "TestUser1" };
        static readonly NetworkUser TestUser2 = new() { Id = 2, Name = "TestUser2", Type = new NetworkUserType() { Name = ObjectType.Group } };

        static Rule Rule1 = new();
        static Rule Rule1Changed = new();
        static Rule Rule2 = new();
        static Rule Rule2Changed = new();
        static Rule NatRule = new();
        static Rule RecertRule1 = new();
        static Rule RecertRule2 = new();

        private const string ToCAnkerIdGroupName = "ToCAnkerId";
        private readonly string ToCRegexPattern = $"<a href=\"#(?'{ToCAnkerIdGroupName}'.*?)\">(.*?)<\\/a>";

        private const string StaticAnkerId = "1234-1234-1234-1234";

        readonly SimulatedUserConfig userConfig = new();
        readonly DynGraphqlQuery query = new("TestFilter")
        {
            ReportTimeString = "2023-04-20T17:50:04",
        };
        readonly TimeFilter timeFilter = new()
        {
            TimeRangeType = TimeRangeType.Fixeddates,
            StartTime = DateTime.Parse("2023-04-19T17:00:04"),
            EndTime = DateTime.Parse("2023-04-20T17:00:04")
        };

        [SetUp]
        public void Initialize()
        {
        }


        [Test]
        public void RulesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting rules report html generation");
            ReportRules reportRules = new(query, userConfig, ReportType.Rules)
            {
                ReportData = ConstructRuleReport(false)
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Rules Report</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>Rules Report</h2><p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Objects</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Services</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Users</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>No.</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td>TestRule1</td><td>srczn</td><td><span style=\"\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x1\" target=\"_top\" style=\"\">TestIp1</a> (1.2.3.4/32)</span><br><span style=\"\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x2\" target=\"_top\" style=\"\">TestIp2</a> (127.0.0.1/32)</span></td><td>dstzn</td><td><span style=\"\"><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x3\" target=\"_top\" style=\"\">TestIpRange</a> (1.2.3.4-1.2.3.5)</span></td><td><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x1\" target=\"_top\" style=\"\">TestService1</a> (443/TCP)</td><td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr><tr><td>2</td><td>TestRule2</td><td></td><td>not<br><span style=\"\"><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x1\" target=\"_top\" style=\"\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x1\" target=\"_top\" style=\"\">TestIp1</a> (1.2.3.4/32)</span><br><span style=\"\"><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x1\" target=\"_top\" style=\"\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x2\" target=\"_top\" style=\"\">TestIp2</a> (127.0.0.1/32)</span></td><td></td><td>not<br><span style=\"\"><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x2\" target=\"_top\" style=\"\">TestUser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x3\" target=\"_top\" style=\"\">TestIpRange</a> (1.2.3.4-1.2.3.5)</span></td><td>not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x2\" target=\"_top\" style=\"\">TestService2</a> (6666-7777/UDP)</td><td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Objects</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>IP Address</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr style=\"\"><td>1</td><td><a name=nwobj1x1>TestIp1</a></td><td>Network</td><td>1.2.3.4/32</td><td></td><td></td><td></td></tr><tr style=\"\"><td>2</td><td><a name=nwobj1x2>TestIp2</a></td><td>Network</td><td>127.0.0.1/32</td><td></td><td></td><td></td></tr><tr style=\"\"><td>3</td><td><a name=nwobj1x3>TestIpRange</a></td><td>IP Range</td><td>1.2.3.4-1.2.3.5</td><td></td><td></td><td></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Services</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>Protocol</th><th>Port</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td><a name=svc1x1>TestService1</a></td><td></td><td>TCP</td><td>443</td><td></td><td></td><td></td></tr><tr><td>2</td><td><a name=svc1x2>TestService2</a></td><td></td><td>UDP</td><td>6666-7777</td><td></td><td></td><td></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Users</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td><a name=user1x1>TestUser1</a></td><td></td><td></td><td></td><td></td></tr><tr><td>2</td><td><a name=user1x2>TestUser2</a></td><td>Group</td><td></td><td></td><td></td></tr></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportRules.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void ResolvedRulesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting rules report resolved html generation");
            ReportRules reportRules = new(query, userConfig, ReportType.ResolvedRules)
            {
                ReportData = ConstructRuleReport(true)
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Rules Report (resolved)</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>Rules Report (resolved)</h2><p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>No.</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td>TestRule1</td><td>srczn</td><td><span style=\"\">TestIp1 (1.2.3.4/32)</span><br><span style=\"\">TestIp2 (127.0.0.1/32)</span></td><td>dstzn</td><td><span style=\"\">TestIpRange (1.2.3.4-1.2.3.5)</span></td><td>TestService1 (443/TCP)</td><td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr><tr><td>2</td><td>TestRule2</td><td></td><td>not<br><span style=\"\">TestUser1@TestIp1 (1.2.3.4/32)</span><br><span style=\"\">TestUser1@TestIp2 (127.0.0.1/32)</span></td><td></td><td>not<br><span style=\"\">TestUser2@TestIpRange (1.2.3.4-1.2.3.5)</span></td><td>not<br>TestService2 (6666-7777/UDP)</td><td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportRules.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void ResolvedRulesTechGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting rules report resolved html generation");
            ReportRules reportRules = new(query, userConfig, ReportType.ResolvedRulesTech)
            {
                ReportData = ConstructRuleReport(true)
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Rules Report (technical)</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>Rules Report (technical)</h2><p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>No.</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td>TestRule1</td><td>srczn</td><td><span style=\"\">1.2.3.4/32</span><br><span style=\"\">127.0.0.1/32</span></td><td>dstzn</td><td><span style=\"\">1.2.3.4-1.2.3.5</span></td><td>443/TCP</td><td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr><tr><td>2</td><td>TestRule2</td><td></td><td>not<br><span style=\"\">TestUser1@1.2.3.4/32</span><br><span style=\"\">TestUser1@127.0.0.1/32</span></td><td></td><td>not<br><span style=\"\">TestUser2@1.2.3.4-1.2.3.5</span></td><td>not<br>6666-7777/UDP</td><td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportRules.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void UnusedRulesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting unused rules report html generation");
            ReportRules reportRules = new(query, userConfig, ReportType.UnusedRules)
            {
                ReportData = ConstructRuleReport(false)
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Unused Rules Report</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>Unused Rules Report</h2><p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Objects</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Services</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Users</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>No.</th><th>Last Hit</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td>2022-04-19</td><td>TestRule1</td><td>srczn</td><td><span style=\"\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x1\" target=\"_top\" style=\"\">TestIp1</a> (1.2.3.4/32)</span><br><span style=\"\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x2\" target=\"_top\" style=\"\">TestIp2</a> (127.0.0.1/32)</span></td><td>dstzn</td><td><span style=\"\"><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x3\" target=\"_top\" style=\"\">TestIpRange</a> (1.2.3.4-1.2.3.5)</span></td><td><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x1\" target=\"_top\" style=\"\">TestService1</a> (443/TCP)</td><td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr><tr><td>2</td><td></td><td>TestRule2</td><td></td><td>not<br><span style=\"\"><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x1\" target=\"_top\" style=\"\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x1\" target=\"_top\" style=\"\">TestIp1</a> (1.2.3.4/32)</span><br><span style=\"\"><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x1\" target=\"_top\" style=\"\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x2\" target=\"_top\" style=\"\">TestIp2</a> (127.0.0.1/32)</span></td><td></td><td>not<br><span style=\"\"><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x2\" target=\"_top\" style=\"\">TestUser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x3\" target=\"_top\" style=\"\">TestIpRange</a> (1.2.3.4-1.2.3.5)</span></td><td>not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x2\" target=\"_top\" style=\"\">TestService2</a> (6666-7777/UDP)</td><td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Objects</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>IP Address</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr style=\"\"><td>1</td><td><a name=nwobj1x1>TestIp1</a></td><td>Network</td><td>1.2.3.4/32</td><td></td><td></td><td></td></tr><tr style=\"\"><td>2</td><td><a name=nwobj1x2>TestIp2</a></td><td>Network</td><td>127.0.0.1/32</td><td></td><td></td><td></td></tr><tr style=\"\"><td>3</td><td><a name=nwobj1x3>TestIpRange</a></td><td>IP Range</td><td>1.2.3.4-1.2.3.5</td><td></td><td></td><td></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Services</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>Protocol</th><th>Port</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td><a name=svc1x1>TestService1</a></td><td></td><td>TCP</td><td>443</td><td></td><td></td><td></td></tr><tr><td>2</td><td><a name=svc1x2>TestService2</a></td><td></td><td>UDP</td><td>6666-7777</td><td></td><td></td><td></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Users</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td><a name=user1x1>TestUser1</a></td><td></td><td></td><td></td><td></td></tr><tr><td>2</td><td><a name=user1x2>TestUser2</a></td><td>Group</td><td></td><td></td><td></td></tr></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportRules.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void RecertReportGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting recert report html generation");
            ReportRules reportRecerts = new(query, userConfig, ReportType.Recertification)
            {
                ReportData = ConstructRecertReport()
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Recertification Report</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>Recertification Report</h2><p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Objects</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Services</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Users</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>No.</th><th>Next Recertification Date</th><th>Owner</th><th>IP address match</th><th>Last Hit</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td><p>1.&nbsp;" + DateOnly.FromDateTime(DateTime.Now.AddDays(5)).ToString("yyyy-MM-dd") + "</p><p style=\"color: red;\">2.&nbsp;" + DateOnly.FromDateTime(DateTime.Now.AddDays(-5)).ToString("yyyy-MM-dd") + "</p></td><td><p>1.&nbsp;TestOwner1</p><p>2.&nbsp;TestOwner2</p></td><td><p>1.&nbsp;TestIp1</p><p>2.&nbsp;TestIp2</p></td><td>2022-04-19</td><td>TestRule1</td><td>srczn</td><td><span style=\"\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x1\" target=\"_top\" style=\"\">TestIp1</a> (1.2.3.4/32)</span><br><span style=\"\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x2\" target=\"_top\" style=\"\">TestIp2</a> (127.0.0.1/32)</span></td><td>dstzn</td><td><span style=\"\"><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x3\" target=\"_top\" style=\"\">TestIpRange</a> (1.2.3.4-1.2.3.5)</span></td><td><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x1\" target=\"_top\" style=\"\">TestService1</a> (443/TCP)</td><td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr><tr><td>2</td><td><p style=\"color: red;\">" + DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd") + "</p></td><td><p>TestOwner1</p></td><td><p>TestIpRange</p></td><td></td><td>TestRule2</td><td></td><td>not<br><span style=\"\"><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x1\" target=\"_top\" style=\"\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x1\" target=\"_top\" style=\"\">TestIp1</a> (1.2.3.4/32)</span><br><span style=\"\"><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x1\" target=\"_top\" style=\"\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x2\" target=\"_top\" style=\"\">TestIp2</a> (127.0.0.1/32)</span></td><td></td><td>not<br><span style=\"\"><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x2\" target=\"_top\" style=\"\">TestUser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x3\" target=\"_top\" style=\"\">TestIpRange</a> (1.2.3.4-1.2.3.5)</span></td><td>not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x2\" target=\"_top\" style=\"\">TestService2</a> (6666-7777/UDP)</td><td>deny</td><td>none</td><td><b>Y</b></td><td>uid2:123</td><td>comment2</td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Objects</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>IP Address</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr style=\"\"><td>1</td><td><a name=nwobj1x1>TestIp1</a></td><td>Network</td><td>1.2.3.4/32</td><td></td><td></td><td></td></tr><tr style=\"\"><td>2</td><td><a name=nwobj1x2>TestIp2</a></td><td>Network</td><td>127.0.0.1/32</td><td></td><td></td><td></td></tr><tr style=\"\"><td>3</td><td><a name=nwobj1x3>TestIpRange</a></td><td>IP Range</td><td>1.2.3.4-1.2.3.5</td><td></td><td></td><td></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Services</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>Protocol</th><th>Port</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td><a name=svc1x1>TestService1</a></td><td></td><td>TCP</td><td>443</td><td></td><td></td><td></td></tr><tr><td>2</td><td><a name=svc1x2>TestService2</a></td><td></td><td>UDP</td><td>6666-7777</td><td></td><td></td><td></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Users</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td><a name=user1x1>TestUser1</a></td><td></td><td></td><td></td><td></td></tr><tr><td>2</td><td><a name=user1x2>TestUser2</a></td><td>Group</td><td></td><td></td><td></td></tr></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportRecerts.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void NatRulesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting nat rules report html generation");
            ReportNatRules reportNatRules = new(query, userConfig, ReportType.NatRules)
            {
                ReportData = ConstructNatRuleReport()
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>NAT Rules Report</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>NAT Rules Report</h2><p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Objects</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Services</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Users</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>No.</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Translated Source</th><th>Translated Destination</th><th>Translated Services</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td>TestRule1</td><td>srczn</td><td><span style=\"\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x1\" target=\"_top\" style=\"\">TestIp1</a> (1.2.3.4/32)</span><br><span style=\"\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x2\" target=\"_top\" style=\"\">TestIp2</a> (127.0.0.1/32)</span></td><td>dstzn</td><td><span style=\"\"><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x3\" target=\"_top\" style=\"\">TestIpRange</a> (1.2.3.4-1.2.3.5)</span></td><td><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x1\" target=\"_top\" style=\"\">TestService1</a> (443/TCP)</td><td><span style=\"\"><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user1x2\" target=\"_top\" style=\"\">TestUser2</a>@<span class=\"oi oi-laptop\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x5\" target=\"_top\" style=\"\">TestIp1Changed</a> (2.3.4.5)</span></td><td>not<br><span style=\"\"><span class=\"oi oi-laptop\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x5\" target=\"_top\" style=\"\">TestIp1Changed</a> (2.3.4.5)</span><br><span style=\"\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x4\" target=\"_top\" style=\"\">TestIpNew</a> (10.0.6.0/24)</span></td><td><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x1\" target=\"_top\" style=\"\">TestService1</a> (443/TCP)<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x2\" target=\"_top\" style=\"\">TestService2</a> (6666-7777/UDP)</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Objects</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>IP Address</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr style=\"\"><td>1</td><td><a name=nwobj1x1>TestIp1</a></td><td>Network</td><td>1.2.3.4/32</td><td></td><td></td><td></td></tr><tr style=\"\"><td>2</td><td><a name=nwobj1x2>TestIp2</a></td><td>Network</td><td>127.0.0.1/32</td><td></td><td></td><td></td></tr><tr style=\"\"><td>3</td><td><a name=nwobj1x3>TestIpRange</a></td><td>IP Range</td><td>1.2.3.4-1.2.3.5</td><td></td><td></td><td></td></tr><tr style=\"\"><td>4</td><td><a name=nwobj1x4>TestIpNew</a></td><td>Network</td><td>10.0.6.0/24</td><td></td><td></td><td></td></tr><tr style=\"\"><td>5</td><td><a name=nwobj1x5>TestIp1Changed</a></td><td>Host</td><td>2.3.4.5</td><td></td><td></td><td></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Services</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>Protocol</th><th>Port</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td><a name=svc1x1>TestService1</a></td><td></td><td>TCP</td><td>443</td><td></td><td></td><td></td></tr><tr><td>2</td><td><a name=svc1x2>TestService2</a></td><td></td><td>UDP</td><td>6666-7777</td><td></td><td></td><td></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Users</h4><table><tr><th>No.</th><th>Name</th><th>Type</th><th>Members</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td><a name=user1x2>TestUser2</a></td><td>Group</td><td></td><td></td><td></td></tr></table><hr></table></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportNatRules.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void ChangesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting changes report html generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.Changes, timeFilter)
            {
                ReportData = ConstructChangeReport(false)
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Changes Report</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>Changes Report</h2><p>Change Time: from: 2023-04-19T15:00:04Z, until: 2023-04-20T15:00:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>Change Time</th><th>Change Type</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>05.04.2023 12:00:00</td><td>Rule added</td><td><p style=\"color: green; text-decoration: bold;\">TestRule1</p></td><td><p style=\"color: green; text-decoration: bold;\">srczn</p></td><td><p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x1\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestIp1</a> (1.2.3.4/32)<br><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x2\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestIp2</a> (127.0.0.1/32)</p></td><td><p style=\"color: green; text-decoration: bold;\">dstzn</p></td><td><p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x3\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestIpRange</a> (1.2.3.4-1.2.3.5)</p></td><td><p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc0x1\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestService1</a> (443/TCP)</p></td><td><p style=\"color: green; text-decoration: bold;\">accept</p></td><td><p style=\"color: green; text-decoration: bold;\">none</p></td><td><p style=\"color: green; text-decoration: bold;\"><b>Y</b></p></td><td><p style=\"color: green; text-decoration: bold;\">uid1</p></td><td><p style=\"color: green; text-decoration: bold;\">comment1</p></td></tr><tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule1</td><td>srczn</td><td><p><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x2\" target=\"_top\" style=\"\">TestIp2</a> (127.0.0.1/32)<br></p>deleted: <p style=\"color: red; text-decoration: line-through red;\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x1\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestIp1</a> (1.2.3.4/32)<br></p>added: <p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-laptop\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x5\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestIp1Changed</a> (2.3.4.5)</p></td><td>dstzn</td><td><p><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x3\" target=\"_top\" style=\"\">TestIpRange</a> (1.2.3.4-1.2.3.5)<br></p>added: <p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x4\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestIpNew</a> (10.0.6.0/24)</p></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\"><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc0x1\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestService1</a> (443/TCP)<br></p>added: <p style=\"color: green; text-decoration: bold;\">not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc0x1\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestService1</a> (443/TCP)</p></td><td>accept</td><td>none</td><td><b>Y</b></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">uid1<br></p></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">comment1<br></p>added: <p style=\"color: green; text-decoration: bold;\">new comment</p></td></tr><tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule2</td><td></td><td>not<br><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user0x1\" target=\"_top\" style=\"\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x1\" target=\"_top\" style=\"\">TestIp1</a> (1.2.3.4/32)<br><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user0x1\" target=\"_top\" style=\"\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x2\" target=\"_top\" style=\"\">TestIp2</a> (127.0.0.1/32)</td><td></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user0x2\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestUser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x3\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestIpRange</a> (1.2.3.4-1.2.3.5)<br></p>added: <p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user0x2\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestUser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x3\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestIpRange</a> (1.2.3.4-1.2.3.5)</p></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc0x2\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestService2</a> (6666-7777/UDP)<br></p>added: <p style=\"color: green; text-decoration: bold;\"><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc0x2\" target=\"_top\" style=\"color: green; text-decoration: bold;\">TestService2</a> (6666-7777/UDP)</p></td><td>deny</td><td>none</td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\"><b>Y</b><br></p>added: <p style=\"color: green; text-decoration: bold;\"><b>N</b></p></td><td>uid2:123</td><td>comment2</td></tr><tr><td>05.04.2023 12:00:00</td><td>Rule deleted</td><td><p style=\"color: red; text-decoration: line-through red;\">TestRule2</p></td><td></td><td><p style=\"color: red; text-decoration: line-through red;\">not<br><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user0x1\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x1\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestIp1</a> (1.2.3.4/32)<br><span class=\"oi oi-person\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user0x1\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestUser1</a>@<span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x2\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestIp2</a> (127.0.0.1/32)</p></td><td></td><td><p style=\"color: red; text-decoration: line-through red;\">not<br><span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#user0x2\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestUser2</a>@<span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj0x3\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestIpRange</a> (1.2.3.4-1.2.3.5)</p></td><td><p style=\"color: red; text-decoration: line-through red;\">not<br><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc0x2\" target=\"_top\" style=\"color: red; text-decoration: line-through red;\">TestService2</a> (6666-7777/UDP)</p></td><td><p style=\"color: red; text-decoration: line-through red;\">deny</p></td><td><p style=\"color: red; text-decoration: line-through red;\">none</p></td><td><p style=\"color: red; text-decoration: line-through red;\"><b>Y</b></p></td><td><p style=\"color: red; text-decoration: line-through red;\">uid2:123</p></td><td><p style=\"color: red; text-decoration: line-through red;\">comment2</p></td></tr></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void ResolvedChangesGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting changes report resolved html generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChanges, timeFilter)
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Changes Report (resolved)</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>Changes Report (resolved)</h2><p>Change Time: from: 2023-04-19T15:00:04Z, until: 2023-04-20T15:00:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>Change Time</th><th>Change Type</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>05.04.2023 12:00:00</td><td>Rule added</td><td><p style=\"color: green; text-decoration: bold;\">TestRule1</p></td><td><p style=\"color: green; text-decoration: bold;\">srczn</p></td><td><p style=\"color: green; text-decoration: bold;\">TestIp1 (1.2.3.4/32)<br>TestIp2 (127.0.0.1/32)</p></td><td><p style=\"color: green; text-decoration: bold;\">dstzn</p></td><td><p style=\"color: green; text-decoration: bold;\">TestIpRange (1.2.3.4-1.2.3.5)</p></td><td><p style=\"color: green; text-decoration: bold;\">TestService1 (443/TCP)</p></td><td><p style=\"color: green; text-decoration: bold;\">accept</p></td><td><p style=\"color: green; text-decoration: bold;\">none</p></td><td><p style=\"color: green; text-decoration: bold;\"><b>Y</b></p></td><td><p style=\"color: green; text-decoration: bold;\">uid1</p></td><td><p style=\"color: green; text-decoration: bold;\">comment1</p></td></tr><tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule1</td><td>srczn</td><td><p>TestIp2 (127.0.0.1/32)<br></p>deleted: <p style=\"color: red; text-decoration: line-through red;\">TestIp1 (1.2.3.4/32)<br></p>added: <p style=\"color: green; text-decoration: bold;\">TestIp1Changed (2.3.4.5)</p></td><td>dstzn</td><td><p>TestIpRange (1.2.3.4-1.2.3.5)<br></p>added: <p style=\"color: green; text-decoration: bold;\">TestIpNew (10.0.6.0/24)</p></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">TestService1 (443/TCP)<br></p>added: <p style=\"color: green; text-decoration: bold;\">not<br>TestService1 (443/TCP)</p></td><td>accept</td><td>none</td><td><b>Y</b></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">uid1<br></p></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">comment1<br></p>added: <p style=\"color: green; text-decoration: bold;\">new comment</p></td></tr><tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule2</td><td></td><td>not<br>TestUser1@TestIp1 (1.2.3.4/32)<br>TestUser1@TestIp2 (127.0.0.1/32)</td><td></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br>TestUser2@TestIpRange (1.2.3.4-1.2.3.5)<br></p>added: <p style=\"color: green; text-decoration: bold;\">TestUser2@TestIpRange (1.2.3.4-1.2.3.5)</p></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br>TestService2 (6666-7777/UDP)<br></p>added: <p style=\"color: green; text-decoration: bold;\">TestService2 (6666-7777/UDP)</p></td><td>deny</td><td>none</td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\"><b>Y</b><br></p>added: <p style=\"color: green; text-decoration: bold;\"><b>N</b></p></td><td>uid2:123</td><td>comment2</td></tr><tr><td>05.04.2023 12:00:00</td><td>Rule deleted</td><td><p style=\"color: red; text-decoration: line-through red;\">TestRule2</p></td><td></td><td><p style=\"color: red; text-decoration: line-through red;\">not<br>TestUser1@TestIp1 (1.2.3.4/32)<br>TestUser1@TestIp2 (127.0.0.1/32)</p></td><td></td><td><p style=\"color: red; text-decoration: line-through red;\">not<br>TestUser2@TestIpRange (1.2.3.4-1.2.3.5)</p></td><td><p style=\"color: red; text-decoration: line-through red;\">not<br>TestService2 (6666-7777/UDP)</p></td><td><p style=\"color: red; text-decoration: line-through red;\">deny</p></td><td><p style=\"color: red; text-decoration: line-through red;\">none</p></td><td><p style=\"color: red; text-decoration: line-through red;\"><b>Y</b></p></td><td><p style=\"color: red; text-decoration: line-through red;\">uid2:123</p></td><td><p style=\"color: red; text-decoration: line-through red;\">comment2</p></td></tr></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void ResolvedChangesTechGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting changes report tech html generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChangesTech, timeFilter)
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Changes Report (technical)</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>Changes Report (technical)</h2><p>Change Time: from: 2023-04-19T15:00:04Z, until: 2023-04-20T15:00:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>Change Time</th><th>Change Type</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>05.04.2023 12:00:00</td><td>Rule added</td><td><p style=\"color: green; text-decoration: bold;\">TestRule1</p></td><td><p style=\"color: green; text-decoration: bold;\">srczn</p></td><td><p style=\"color: green; text-decoration: bold;\">1.2.3.4/32<br>127.0.0.1/32</p></td><td><p style=\"color: green; text-decoration: bold;\">dstzn</p></td><td><p style=\"color: green; text-decoration: bold;\">1.2.3.4-1.2.3.5</p></td><td><p style=\"color: green; text-decoration: bold;\">443/TCP</p></td><td><p style=\"color: green; text-decoration: bold;\">accept</p></td><td><p style=\"color: green; text-decoration: bold;\">none</p></td><td><p style=\"color: green; text-decoration: bold;\"><b>Y</b></p></td><td><p style=\"color: green; text-decoration: bold;\">uid1</p></td><td><p style=\"color: green; text-decoration: bold;\">comment1</p></td></tr><tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule1</td><td>srczn</td><td><p>127.0.0.1/32<br></p>deleted: <p style=\"color: red; text-decoration: line-through red;\">1.2.3.4/32<br></p>added: <p style=\"color: green; text-decoration: bold;\">2.3.4.5</p></td><td>dstzn</td><td><p>1.2.3.4-1.2.3.5<br></p>added: <p style=\"color: green; text-decoration: bold;\">10.0.6.0/24</p></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">443/TCP<br></p>added: <p style=\"color: green; text-decoration: bold;\">not<br>443/TCP</p></td><td>accept</td><td>none</td><td><b>Y</b></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">uid1<br></p></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">comment1<br></p>added: <p style=\"color: green; text-decoration: bold;\">new comment</p></td></tr><tr><td>05.04.2023 12:00:00</td><td>Rule modified</td><td>TestRule2</td><td></td><td>not<br>TestUser1@1.2.3.4/32<br>TestUser1@127.0.0.1/32</td><td></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br>TestUser2@1.2.3.4-1.2.3.5<br></p>added: <p style=\"color: green; text-decoration: bold;\">TestUser2@1.2.3.4-1.2.3.5</p></td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\">not<br>6666-7777/UDP<br></p>added: <p style=\"color: green; text-decoration: bold;\">6666-7777/UDP</p></td><td>deny</td><td>none</td><td>deleted: <p style=\"color: red; text-decoration: line-through red;\"><b>Y</b><br></p>added: <p style=\"color: green; text-decoration: bold;\"><b>N</b></p></td><td>uid2:123</td><td>comment2</td></tr><tr><td>05.04.2023 12:00:00</td><td>Rule deleted</td><td><p style=\"color: red; text-decoration: line-through red;\">TestRule2</p></td><td></td><td><p style=\"color: red; text-decoration: line-through red;\">not<br>TestUser1@1.2.3.4/32<br>TestUser1@127.0.0.1/32</p></td><td></td><td><p style=\"color: red; text-decoration: line-through red;\">not<br>TestUser2@1.2.3.4-1.2.3.5</p></td><td><p style=\"color: red; text-decoration: line-through red;\">not<br>6666-7777/UDP</p></td><td><p style=\"color: red; text-decoration: line-through red;\">deny</p></td><td><p style=\"color: red; text-decoration: line-through red;\">none</p></td><td><p style=\"color: red; text-decoration: line-through red;\"><b>Y</b></p></td><td><p style=\"color: red; text-decoration: line-through red;\">uid2:123</p></td><td><p style=\"color: red; text-decoration: line-through red;\">comment2</p></td></tr></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public async Task AppRulesGenerateHtml()
        {
            // TODO: to be enhanced
            Log.WriteInfo("Test Log", "starting AppRules report html generation");
            ReportAppRules reportAppRules = new(query, userConfig, ReportType.AppRules, new())
            {
                ReportData = await ConstructAppRulesReport()
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>App Rules</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>App Rules</h2><p>Time of configuration: 2023-04-20T15:50:04Z (UTC)</p><p>Generated on: Z (UTC)</p><p>Devices: TestMgt [TestDev]</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestMgt</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">TestDev</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestMgt</h3><hr><h4 id=\"" + StaticAnkerId + "\">TestDev</h4><table><tr><th>No.</th><th>Last Hit</th><th>Name</th><th>Source Zone</th><th>Source</th><th>Destination Zone</th><th>Destination</th><th>Services</th><th>Action</th><th>Track</th><th>Enabled</th><th>Uid</th><th>Comment</th></tr><tr><td>1</td><td>2022-04-19</td><td>TestRule1</td><td>srczn</td><td><span style=\" color: red;\"><span class=\"oi oi-rss\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x1\" target=\"_top\" style=\" color: red;\">TestIp1</a> (1.2.3.4/32)</span><br><span class=\"text-secondary\">... (1 more)</span></td><td>dstzn</td><td><span style=\" color: red;\"><span class=\"oi oi-resize-width\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x3\" target=\"_top\" style=\" color: red;\">TestIpRange</a> (1.2.3.4-1.2.3.5)</span></td><td><span class=\"oi oi-wrench\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x1\" target=\"_top\" style=\"\">TestService1</a> (443/TCP)</td><td>accept</td><td>none</td><td><b>Y</b></td><td>uid1</td><td>comment1</td></tr></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportAppRules.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void ConnectionsGenerateHtml()
        {
            Log.WriteInfo("Test Log", "starting connection report html generation");
            ReportConnections reportConnections = new(query, userConfig, ReportType.Connections)
            {
                ReportData = ConstructConnectionReport()
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Connections Report</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>Connections Report</h2><p>Generated on: Z (UTC)</p><p>Owners: TestOwner</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestOwner</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Connections</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Interfaces</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Own Common Services</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Objects</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Services</a></li></ul><li><a href=\"#" + StaticAnkerId + "\">Global Common Services</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Objects</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Services</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr><h3 id=\"" + StaticAnkerId + "\">TestOwner</h3><h4 id=\"" + StaticAnkerId + "\">Connections</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Functional Reason</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td>1</td><td>101</td><td>Conn1</td><td></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x11\" target=\"_top\" style=\"\">AppServer1 (1.0.0.0)</a></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x41\" target=\"_top\" style=\"\">ServiceGroup1</a><br><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x31\" target=\"_top\" style=\"\">Service1 (1234/TCP)</a></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x21\" target=\"_top\" style=\"\">AppRole1 (AR1)</a></td></table><hr><h4 id=\"" + StaticAnkerId + "\">Interfaces</h4><table><tr><th>No.</th><th>Id</th><th>Published</th><th>Name</th><th>Interface Description</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td>1</td><td>102</td><td>✖</td><td>Inter2</td><td></td><td></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x0\" target=\"_top\" style=\"\"></a><br><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x32\" target=\"_top\" style=\"\">Service2 (2345/UDP)</a></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x0\" target=\"_top\" style=\"\">noRole ()</a><br><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x12\" target=\"_top\" style=\"\">AppServer2 (2.0.0.0)</a></td></table><hr><h4 id=\"" + StaticAnkerId + "\">Own Common Services</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Functional Reason</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td>1</td><td>103</td><td>ComSvc3</td><td></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x11\" target=\"_top\" style=\"\">AppServer1 (1.0.0.0)</a></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x0\" target=\"_top\" style=\"\"></a><br><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc1x32\" target=\"_top\" style=\"\">Service2 (2345/UDP)</a></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj1x12\" target=\"_top\" style=\"\">AppServer2 (2.0.0.0)</a></td></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Objects</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Ip</th><th>Members</th></tr><tr><td>1</td><td>21</td><td><a name=nwobj1x21>AppRole1 (AR1)</a></td><td></td><td>AppServer1</td><tr><td>2</td><td>11</td><td><a name=nwobj1x11>AppServer1</a></td><td>1.0.0.0</td><td></td><tr><td>3</td><td>0</td><td><a name=nwobj1x0>noRole ()</a></td><td></td><td></td><tr><td>4</td><td>12</td><td><a name=nwobj1x12>AppServer2</a></td><td>2.0.0.0</td><td></td></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Services</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Protocol</th><th>Port</th><th>Members</th></tr><tr><td>1</td><td>41</td><td><a name=svc1x41>ServiceGroup1</a></td><td></td><td></td><td>Service1</td><tr><td>2</td><td>31</td><td><a name=svc1x31>Service1</a></td><td>TCP</td><td>1234</td><td></td><tr><td>3</td><td>0</td><td><a name=svc1x0></a></td><td></td><td></td><td></td><tr><td>4</td><td>32</td><td><a name=svc1x32>Service2</a></td><td>UDP</td><td>2345</td><td></td></table><hr><hr><h3 id=\"" + StaticAnkerId + "\">Global Common Services</h3><table><tr><th>No.</th><th>Id</th><th>Owner</th><th>Name</th><th>Functional Reason</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td>1</td><td>103</td><td>App1</td><td>ComSvc3</td><td></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj2x11\" target=\"_top\" style=\"\">AppServer1 (1.0.0.0)</a></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc2x0\" target=\"_top\" style=\"\"></a><br><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#svc2x32\" target=\"_top\" style=\"\">Service2 (2345/UDP)</a></td><td><span class=\"\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"#nwobj2x12\" target=\"_top\" style=\"\">AppServer2 (2.0.0.0)</a></td></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Objects</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Ip</th><th>Members</th></tr><tr><td>1</td><td>11</td><td><a name=nwobj2x11>AppServer1</a></td><td>1.0.0.0</td><td></td><tr><td>2</td><td>12</td><td><a name=nwobj2x12>AppServer2</a></td><td>2.0.0.0</td><td></td></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Services</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Protocol</th><th>Port</th><th>Members</th></tr><tr><td>1</td><td>0</td><td><a name=svc2x0></a></td><td></td><td></td><td></td><tr><td>2</td><td>32</td><td><a name=svc2x32>Service2</a></td><td>UDP</td><td>2345</td><td></td></table><hr></body></html>";

            string reportHtml = RemoveLinebreaks(RemoveGenDate(reportConnections.ExportToHtml(), true));

            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);
        }

        [Test]
        public void VariancesGenerateHtml()
        {
            // TODO: to be enhanced
            Log.WriteInfo("Test Log", "starting variance report html generation");
            ReportVariances reportVariances = new(query, userConfig, ReportType.VarianceAnalysis)
            {
                ReportData = ConstructVarianceReport()
            };

            string expectedHtmlResult = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>VarianceAnalysis</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>VarianceAnalysis</h2><p>Generated on: Z (UTC)</p><p>Owners: TestOwner</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestOwner</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">AppRoles Not Implemented</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\"></a></li></ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">AppRoles With Diffs</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\"></a></li></ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Connections not implemented</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Connections</a></li></ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Connections with Diffs</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Conn2</a></li></ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Objects</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Services</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr>In this report...<br><h3 id=\"" + StaticAnkerId + "\">TestOwner</h3><table><tr><th></th><th>fullymodelled</th><th>Implemented</th><th>Not Implemented</th><th>With Diffs</th></tr><tr><td>AppRoles</td><td>2</td><td>0</td><td>1</td><td>1</td></tr><tr><td>Connections</td><td>2</td><td>0</td><td>1</td><td>1</td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">AppRoles Not Implemented</h4><h5 id=\"" + StaticAnkerId + "\"></h5><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Members</th></tr><tr><td>1</td><td>21</td><td>AppRole1</td><td>AppServer1 (1.0.0.0)</td></table><hr><hr><h4 id=\"" + StaticAnkerId + "\">AppRoles With Diffs</h4><h5 id=\"" + StaticAnkerId + "\"></h5><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Missing App Servers</th><th>Surplus App Servers</th></tr><tr><td>1</td><td>22</td><td>AppRole2</td><td>AppServer2 (2.0.0.0)</td><td></td></table><hr><hr><h4 id=\"" + StaticAnkerId + "\">Connections not implemented</h4><h5 id=\"" + StaticAnkerId + "\">Connections</h5><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Functional Reason</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td>1</td><td>101</td><td>Conn1</td><td></td><td>Area50 (NA50)<br>AppRole1 (AR1)<br>AppServer1 (1.0.0.0)</td><td>ServiceGroup1<br>Service1 (1234/TCP)</td><td>AppRole2 (AR2)</td></table><hr><h4 id=\"" + StaticAnkerId + "\">Connections with Diffs</h4><h5 id=\"" + StaticAnkerId + "\">Conn2</h5><table><tr><th>Id</th><th>Name</th><th>Functional Reason</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td>102</td><td>Conn2</td><td></td><td>AppServer1 (1.0.0.0)</td><td>Service1 (1234/TCP)</td><td>AppRole2 (AR2)</td></table><table><tr><th>Management</th><th>Gateway</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td></td><td></td><td><p><span class=\"oi oi-laptop\"></span><br></p></td><td><p><span class=\"oi oi-wrench\"> ()</span><br></p></td><td><p><span class=\"oi oi-laptop\"></span><br></p></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Objects</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Ip</th><th>Members</th></tr><tr><td>1</td><td>51</td><td><a name=nwobj0x51>Area50 (NA50)</a></td><td></td><td>...</td><tr><td>2</td><td>21</td><td><a name=nwobj0x21>AppRole1 (AR1)</a></td><td></td><td>AppServer1</td><tr><td>3</td><td>11</td><td><a name=nwobj0x11>AppServer1</a></td><td>1.0.0.0</td><td></td><tr><td>4</td><td>22</td><td><a name=nwobj0x22>AppRole2 (AR2)</a></td><td></td><td>AppServer2</td><tr><td>5</td><td>12</td><td><a name=nwobj0x12>AppServer2</a></td><td>2.0.0.0</td><td></td></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Services</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Protocol</th><th>Port</th><th>Members</th></tr><tr><td>1</td><td>41</td><td><a name=svc0x41>ServiceGroup1</a></td><td></td><td></td><td>Service1</td><tr><td>2</td><td>31</td><td><a name=svc0x31>Service1</a></td><td>TCP</td><td>1234</td><td></td></table><hr><hr></body></html>";
             string reportHtml = RemoveLinebreaks(RemoveGenDate(reportVariances.ExportToHtml(), true));
            IEnumerable<string> matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult, reportHtml);

            userConfig.ResolveNetworkAreas = true;
            reportVariances = new(query, userConfig, ReportType.VarianceAnalysis)
            {
                ReportData = ConstructVarianceReport()
            };
            string expectedHtmlResult2 = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>VarianceAnalysis</title><style>table {font-family: arial, sans-serif;font-size: 10px;border-collapse: collapse;width: 100 %;}td {border: 1px solid #000000;text-align: left;padding: 3px;}th {border: 1px solid #000000;text-align: left;padding: 3px;background-color: #dddddd;}</style></head><body><h2>VarianceAnalysis</h2><p>Generated on: Z (UTC)</p><p>Owners: TestOwner</p><p>Filter: TestFilter</p><hr><div id=\"toc_container\"><h2>Table of content</h2><ul class=\"toc_list\"><li><a href=\"#" + StaticAnkerId + "\">TestOwner</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">AppRoles Not Implemented</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\"></a></li></ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">AppRoles With Diffs</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\"></a></li></ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Connections not implemented</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Connections</a></li></ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Connections with Diffs</a></li><ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Conn2</a></li></ul><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Objects</a></li><li class=\"subli\"><a href=\"#" + StaticAnkerId + "\">Network Services</a></li></ul></ul></div><style>#toc_container {background: #f9f9f9 none repeat scroll 0 0;border: 1px solid #aaa;display: table;font-size: 95%;margin-bottom: 1em;padding: 10px;width: 100%;}#toc_container ul{list-style-type: none;}.subli {list-style-type: square;}.toc_list ul li {margin-bottom: 4px;}.toc_list a {color: black;font-family: 'Arial';font-size: 12pt;}</style><hr>In this report...<br><h3 id=\"" + StaticAnkerId + "\">TestOwner</h3><table><tr><th></th><th>fullymodelled</th><th>Implemented</th><th>Not Implemented</th><th>With Diffs</th></tr><tr><td>AppRoles</td><td>2</td><td>0</td><td>1</td><td>1</td></tr><tr><td>Connections</td><td>2</td><td>0</td><td>1</td><td>1</td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">AppRoles Not Implemented</h4><h5 id=\"" + StaticAnkerId + "\"></h5><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Members</th></tr><tr><td>1</td><td>21</td><td>AppRole1</td><td>AppServer1 (1.0.0.0)</td></table><hr><hr><h4 id=\"" + StaticAnkerId + "\">AppRoles With Diffs</h4><h5 id=\"" + StaticAnkerId + "\"></h5><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Missing App Servers</th><th>Surplus App Servers</th></tr><tr><td>1</td><td>22</td><td>AppRole2</td><td>AppServer2 (2.0.0.0)</td><td></td></table><hr><hr><h4 id=\"" + StaticAnkerId + "\">Connections not implemented</h4><h5 id=\"" + StaticAnkerId + "\">Connections</h5><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Functional Reason</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td>1</td><td>101</td><td>Conn1</td><td></td><td>Area50 (NA50)<br>AppRole1 (AR1)<br>AppServer1 (1.0.0.0)</td><td>ServiceGroup1<br>Service1 (1234/TCP)</td><td>AppRole2 (AR2)</td></table><hr><h4 id=\"" + StaticAnkerId + "\">Connections with Diffs</h4><h5 id=\"" + StaticAnkerId + "\">Conn2</h5><table><tr><th>Id</th><th>Name</th><th>Functional Reason</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td>102</td><td>Conn2</td><td></td><td>AppServer1 (1.0.0.0)</td><td>Service1 (1234/TCP)</td><td>AppRole2 (AR2)</td></table><table><tr><th>Management</th><th>Gateway</th><th>Source</th><th>Services</th><th>Destination</th></tr><tr><td></td><td></td><td><p><span class=\"oi oi-laptop\"></span><br></p></td><td><p><span class=\"oi oi-wrench\"> ()</span><br></p></td><td><p><span class=\"oi oi-laptop\"></span><br></p></td></tr></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Objects</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Ip</th><th>Members</th></tr><tr><td>1</td><td>51</td><td><a name=nwobj0x51>Area50 (NA50)</a></td><td></td><td>Net1</td><tr><td>2</td><td>1</td><td><a name=nwobj0x1>Net1</a></td><td>1.0.0.0</td><td></td><tr><td>3</td><td>21</td><td><a name=nwobj0x21>AppRole1 (AR1)</a></td><td></td><td>AppServer1</td><tr><td>4</td><td>11</td><td><a name=nwobj0x11>AppServer1</a></td><td>1.0.0.0</td><td></td><tr><td>5</td><td>22</td><td><a name=nwobj0x22>AppRole2 (AR2)</a></td><td></td><td>AppServer2</td><tr><td>6</td><td>12</td><td><a name=nwobj0x12>AppServer2</a></td><td>2.0.0.0</td><td></td></table><hr><h4 id=\"" + StaticAnkerId + "\">Network Services</h4><table><tr><th>No.</th><th>Id</th><th>Name</th><th>Protocol</th><th>Port</th><th>Members</th></tr><tr><td>1</td><td>41</td><td><a name=svc0x41>ServiceGroup1</a></td><td></td><td></td><td>Service1</td><tr><td>2</td><td>31</td><td><a name=svc0x31>Service1</a></td><td>TCP</td><td>1234</td><td></td></table><hr><hr></body></html>";
            reportHtml = RemoveLinebreaks(RemoveGenDate(reportVariances.ExportToHtml(), true));
            matches = reportHtml.GetMatches(ToCRegexPattern, ToCAnkerIdGroupName);
            reportHtml = reportHtml.ReplaceAll(matches, StaticAnkerId);

            ClassicAssert.AreEqual(expectedHtmlResult2, reportHtml);
        }

        [Test]
        public void ResolvedRulesGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting rules report resolved csv generation");
            ReportRules reportRules = new(query, userConfig, ReportType.ResolvedRules)
            {
                ReportData = ConstructRuleReport(true)
            };

            string expectedCsvResult = "# report type: Rules Report (resolved)" +
            "# report generation date: Z (UTC)" +
            "# date of configuration shown: 2023-04-20T15:50:04Z (UTC)" +
            "# device filter: TestMgt [TestDev]" +
            "# other filters: TestFilter" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en" +
            "# data protection level: For internal use only#" +
            "\"management-name\",\"device-name\",\"rule-number\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"" +
            "\"TestMgt\",\"TestDev\",\"1\",\"TestRule1\",\"srczn\",\"TestIp1 (1.2.3.4/32),TestIp2 (127.0.0.1/32)\",\"dstzn\",\"TestIpRange (1.2.3.4-1.2.3.5)\",\"TestService1 (443/TCP)\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\"" +
            "\"TestMgt\",\"TestDev\",\"2\",\"TestRule2\",\"\",\"not(TestUser1@TestIp1 (1.2.3.4/32),TestUser1@TestIp2 (127.0.0.1/32))\",\"\",\"not(TestUser2@TestIpRange (1.2.3.4-1.2.3.5))\",\"not(TestService2 (6666-7777/UDP))\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\"";
            ClassicAssert.AreEqual(expectedCsvResult, RemoveLinebreaks(RemoveGenDate(reportRules.ExportToCsv())));
        }

        [Test]
        public void ResolvedRulesTechGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting rules report tech csv generation");
            ReportRules reportRules = new(query, userConfig, ReportType.ResolvedRulesTech)
            {
                ReportData = ConstructRuleReport(true)
            };

            string expectedCsvResult = "# report type: Rules Report (technical)" +
            "# report generation date: Z (UTC)" +
            "# date of configuration shown: 2023-04-20T15:50:04Z (UTC)" +
            "# device filter: TestMgt [TestDev]" +
            "# other filters: TestFilter" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en" +
            "# data protection level: For internal use only#" +
            "\"management-name\",\"device-name\",\"rule-number\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"" +
            "\"TestMgt\",\"TestDev\",\"1\",\"TestRule1\",\"srczn\",\"1.2.3.4/32,127.0.0.1/32\",\"dstzn\",\"1.2.3.4-1.2.3.5\",\"443/TCP\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\"" +
            "\"TestMgt\",\"TestDev\",\"2\",\"TestRule2\",\"\",\"not(TestUser1@1.2.3.4/32,TestUser1@127.0.0.1/32)\",\"\",\"not(TestUser2@1.2.3.4-1.2.3.5)\",\"not(6666-7777/UDP)\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\"";
            ClassicAssert.AreEqual(expectedCsvResult, RemoveLinebreaks(RemoveGenDate(reportRules.ExportToCsv())));
        }

        [Test]
        public void ResolvedChangesGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting changes report resolved csv generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChanges, new TimeFilter())
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedCsvResult = "# report type: Changes Report (resolved)" +
            "# report generation date: Z (UTC)" +
            "# device filter: TestMgt [TestDev]" +
            "# other filters: TestFilter" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en" +
            "# data protection level: For internal use only#" +
            "\"management-name\",\"device-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule added\",\"TestRule1\",\"srczn\",\"TestIp1 (1.2.3.4/32),TestIp2 (127.0.0.1/32)\"," +
            "\"dstzn\",\"TestIpRange (1.2.3.4-1.2.3.5)\",\"TestService1 (443/TCP)\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\"" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule1\",\"srczn\",\"TestIp2 (127.0.0.1/32) deleted: TestIp1 (1.2.3.4/32) added: TestIp1Changed (2.3.4.5)\"," +
            "\"dstzn\",\"TestIpRange (1.2.3.4-1.2.3.5) added: TestIpNew (10.0.6.0/24)\"," +
            "\" deleted: TestService1 (443/TCP) added: not(TestService1 (443/TCP))\",\"accept\",\"none\",\"enabled\",\" deleted: uid1\",\" deleted: comment1 added: new comment\"" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule2\",\"\",\"not(TestUser1@TestIp1 (1.2.3.4/32),TestUser1@TestIp2 (127.0.0.1/32))\"," +
            "\"\",\" deleted: not(TestUser2@TestIpRange (1.2.3.4-1.2.3.5)) added: TestUser2@TestIpRange (1.2.3.4-1.2.3.5)\"," +
            "\" deleted: not(TestService2 (6666-7777/UDP)) added: TestService2 (6666-7777/UDP)\",\"deny\",\"none\",\" deleted: enabled added: disabled\",\"uid2:123\",\"comment2\"" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule deleted\",\"TestRule2\",\"\",\"not(TestUser1@TestIp1 (1.2.3.4/32),TestUser1@TestIp2 (127.0.0.1/32))\"," +
            "\"\",\"not(TestUser2@TestIpRange (1.2.3.4-1.2.3.5))\",\"not(TestService2 (6666-7777/UDP))\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\"";
            ClassicAssert.AreEqual(expectedCsvResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToCsv())));
        }

        [Test]
        public void ResolvedChangesTechGenerateCsv()
        {
            Log.WriteInfo("Test Log", "starting changes report tech csv generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChangesTech, new TimeFilter())
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedCsvResult = "# report type: Changes Report (technical)" +
            "# report generation date: Z (UTC)" +
            "# device filter: TestMgt [TestDev]" +
            "# other filters: TestFilter" +
            "# report generator: Firewall Orchestrator - https://fwo.cactus.de/en" +
            "# data protection level: For internal use only#" +
            "\"management-name\",\"device-name\",\"change-time\",\"change-type\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule added\",\"TestRule1\",\"srczn\",\"1.2.3.4/32,127.0.0.1/32\",\"dstzn\",\"1.2.3.4-1.2.3.5\",\"443/TCP\",\"accept\",\"none\",\"enabled\",\"uid1\",\"comment1\"" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule1\",\"srczn\",\"127.0.0.1/32 deleted: 1.2.3.4/32 added: 2.3.4.5\",\"dstzn\",\"1.2.3.4-1.2.3.5 added: 10.0.6.0/24\",\" deleted: 443/TCP added: not(443/TCP)\",\"accept\",\"none\",\"enabled\",\" deleted: uid1\",\" deleted: comment1 added: new comment\"" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule modified\",\"TestRule2\",\"\",\"not(TestUser1@1.2.3.4/32,TestUser1@127.0.0.1/32)\",\"\",\" deleted: not(TestUser2@1.2.3.4-1.2.3.5) added: TestUser2@1.2.3.4-1.2.3.5\",\" deleted: not(6666-7777/UDP) added: 6666-7777/UDP\",\"deny\",\"none\",\" deleted: enabled added: disabled\",\"uid2:123\",\"comment2\"" +
            "\"TestMgt\",\"TestDev\",\"05.04.2023 12:00:00\",\"Rule deleted\",\"TestRule2\",\"\",\"not(TestUser1@1.2.3.4/32,TestUser1@127.0.0.1/32)\",\"\",\"not(TestUser2@1.2.3.4-1.2.3.5)\",\"not(6666-7777/UDP)\",\"deny\",\"none\",\"enabled\",\"uid2:123\",\"comment2\"";
            ClassicAssert.AreEqual(expectedCsvResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToCsv())));
        }


        [Test]
        public void RulesGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting rules report json generation");
            ReportRules reportRules = new(query, userConfig, ReportType.Rules)
            {
                ReportData = ConstructRuleReport(false)
            };

            string expectedJsonResult =
            "[{\"id\": 0,\"name\": \"TestMgt\"," +
            "\"devices\": [{\"id\": 0,\"name\": \"TestDev\"," +
            "\"rules\": [{\"rule_id\": 0,\"rule_uid\": \"uid1\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"TestRule1\",\"rule_comment\": \"comment1\",\"rule_disabled\": false," +
            "\"rule_services\": [{\"service\": {\"svc_id\": 1,\"svc_name\": \"TestService1\",\"svc_uid\": \"\",\"svc_port\": 443,\"svc_port_end\": 443,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0," +
            "\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0," +
            "\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 6,\"name\": \"TCP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}}]," +
            "\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_src_neg\": false,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"srczn\"}," +
            "\"rule_froms\": [{\"object\": {\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}," +
            "{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_dst_neg\": false,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"dstzn\"}," +
            "\"rule_tos\": [{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"ip_range\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_action\": \"accept\",\"rule_track\": \"none\",\"section_header\": \"\"," +
            "\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": \"2022-04-19T00:00:00\",\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []},\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 1,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false}," +
            "{\"rule_id\": 0,\"rule_uid\": \"uid2:123\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"TestRule2\",\"rule_comment\": \"comment2\",\"rule_disabled\": false," +
            "\"rule_services\": [{\"service\": {\"svc_id\": 2,\"svc_name\": \"TestService2\",\"svc_uid\": \"\",\"svc_port\": 6666,\"svc_port_end\": 7777,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0," +
            "\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 17,\"name\": \"UDP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}}]," +
            "\"rule_svc_neg\": true,\"rule_svc\": \"\",\"rule_src_neg\": true,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"\"}," +
            "\"rule_froms\": [{\"object\": {\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}," +
            "{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_dst_neg\": true,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"\"}," +
            "\"rule_tos\": [{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"ip_range\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 2,\"user_uid\": \"\",\"user_name\": \"TestUser2\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"group\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_action\": \"deny\",\"rule_track\": \"none\",\"section_header\": \"\"," +
            "\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": null,\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []},\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 2,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false}],\"changelog_rules\": null,\"rules_aggregate\": {\"aggregate\": {\"count\": 0}}}]," +
            "\"import\": {\"aggregate\": {\"max\": {\"id\": null}}},\"RelevantImportId\": null," +
            "\"networkObjects\": [],\"serviceObjects\": [],\"userObjects\": []," +
            "\"reportNetworkObjects\": [{\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "{\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "{\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"ip_range\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}]," +
            "\"reportServiceObjects\": [{\"svc_id\": 1,\"svc_name\": \"TestService1\",\"svc_uid\": \"\",\"svc_port\": 443,\"svc_port_end\": 443,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0," +
            "\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 6,\"name\": \"TCP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}," +
            "{\"svc_id\": 2,\"svc_name\": \"TestService2\",\"svc_uid\": \"\",\"svc_port\": 6666,\"svc_port_end\": 7777,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0," +
            "\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 17,\"name\": \"UDP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}]," +
            "\"reportUserObjects\": [{\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}," +
            "{\"user_id\": 2,\"user_uid\": \"\",\"user_name\": \"TestUser2\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"group\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}]," +
            "\"ReportedRuleIds\": [],\"ReportedNetworkServiceIds\": [],\"objects_aggregate\": {\"aggregate\": {\"count\": 0}},\"services_aggregate\": {\"aggregate\": {\"count\": 0}},\"usrs_aggregate\": {\"aggregate\": {\"count\": 0}},\"rules_aggregate\": {\"aggregate\": {\"count\": 0}}," +
            "\"Ignore\": false}]";
            // Log.WriteInfo("Test Log", removeLinebreaks((removeGenDate(reportRules.ExportToJson(), true, true))));
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportRules.ExportToJson(), false, true)));
        }

        [Test]
        public void ResolvedRulesGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting resolved rules report json generation");
            ReportRules reportRules = new(query, userConfig, ReportType.ResolvedRules)
            {
                ReportData = ConstructRuleReport(true)
            };

            string expectedJsonResult =
            "{\"report type\": \"Rules Report (resolved)\",\"report generation date\": \"Z (UTC)\"," +
            "\"date of configuration shown\": \"2023-04-20T15:50:04Z (UTC)\",\"device filter\": \"TestMgt [TestDev]\",\"other filters\": \"TestFilter\"," +
            "\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"gateways\": [{\"TestDev\": {" +
            "\"rules\": [{\"number\": 1,\"name\": \"TestRule1\",\"source zone\": \"srczn\",\"source negated\": false," +
            "\"source\": [\"TestIp1 (1.2.3.4/32)\",\"TestIp2 (127.0.0.1/32)\"],\"destination zone\": \"dstzn\",\"destination negated\": false," +
            "\"destination\": [\"TestIpRange (1.2.3.4-1.2.3.5)\"],\"service negated\": false," +
            "\"service\": [\"TestService1 (443/TCP)\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"number\": 2,\"name\": \"TestRule2\",\"source zone\": \"\",\"source negated\": true," +
            "\"source\": [\"TestUser1@TestIp1 (1.2.3.4/32)\",\"TestUser1@TestIp2 (127.0.0.1/32)\"],\"destination zone\": \"\",\"destination negated\": true," +
            "\"destination\": [\"TestUser2@TestIpRange (1.2.3.4-1.2.3.5)\"],\"service negated\": true," +
            "\"service\": [\"TestService2 (6666-7777/UDP)\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]}}]}}]}";
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportRules.ExportToJson(), false, true)));
        }

        [Test]
        public void ResolvedRulesTechGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting resolved rules report tech json generation");
            ReportRules reportRules = new(query, userConfig, ReportType.ResolvedRulesTech)
            {
                ReportData = ConstructRuleReport(true)
            };

            string expectedJsonResult =
            "{\"report type\": \"Rules Report (technical)\",\"report generation date\": \"Z (UTC)\"," +
            "\"date of configuration shown\": \"2023-04-20T15:50:04Z (UTC)\"," +
            "\"device filter\": \"TestMgt [TestDev]\",\"other filters\": \"TestFilter\"," +
            "\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"gateways\": [{\"TestDev\": {" +
            "\"rules\": [{\"number\": 1,\"name\": \"TestRule1\",\"source zone\": \"srczn\",\"source negated\": false," +
            "\"source\": [\"1.2.3.4/32\",\"127.0.0.1/32\"],\"destination zone\": \"dstzn\",\"destination negated\": false," +
            "\"destination\": [\"1.2.3.4-1.2.3.5\"],\"service negated\": false," +
            "\"service\": [\"443/TCP\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"number\": 2,\"name\": \"TestRule2\",\"source zone\": \"\",\"source negated\": true," +
            "\"source\": [\"TestUser1@1.2.3.4/32\",\"TestUser1@127.0.0.1/32\"],\"destination zone\": \"\"," +
            "\"destination negated\": true,\"destination\": [\"TestUser2@1.2.3.4-1.2.3.5\"],\"service negated\": true," +
            "\"service\": [\"6666-7777/UDP\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]}}]}}]}";
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportRules.ExportToJson(), false, true)));
        }

        [Test]
        public void ChangesGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting changes report json generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.Changes, new TimeFilter())
            {
                ReportData = ConstructChangeReport(false)
            };

            string expectedJsonResult =
            "[{\"id\": 0,\"name\": \"TestMgt\"," +
            "\"devices\": [{\"id\": 0,\"name\": \"TestDev\"," +
            "\"rules\": null," +
            "\"changelog_rules\": [{\"import\": {\"time\": \"2023-04-05T12:00:00\"},\"change_action\": \"I\"," +
            "\"old\": {\"rule_id\": 0,\"rule_uid\": \"\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"\",\"rule_comment\": \"\",\"rule_disabled\": false," +
            "\"rule_services\": [],\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_src_neg\": false,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"rule_tos\": [],\"rule_action\": \"\",\"rule_track\": \"\",\"section_header\": \"\"," +
            "\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": null,\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
            "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 0,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false},\"new\": {\"rule_id\": 0,\"rule_uid\": \"uid1\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"TestRule1\",\"rule_comment\": \"comment1\",\"rule_disabled\": false," +
            "\"rule_services\": [{\"service\": {\"svc_id\": 1,\"svc_name\": \"TestService1\",\"svc_uid\": \"\",\"svc_port\": 443,\"svc_port_end\": 443,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 6,\"name\": \"TCP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}}]," +
            "\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_src_neg\": false,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"srczn\"}," +
            "\"rule_froms\": [{\"object\": {\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}," +
            "{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_dst_neg\": false,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"dstzn\"},\"rule_tos\": [{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"ip_range\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_action\": \"accept\",\"rule_track\": \"none\",\"section_header\": \"\",\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": \"2022-04-19T00:00:00\",\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
            "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 1,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false},\"DeviceName\": \"\"}," +
            "{\"import\": {\"time\": \"2023-04-05T12:00:00\"},\"change_action\": \"C\",\"old\": {\"rule_id\": 0,\"rule_uid\": \"uid1\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"TestRule1\",\"rule_comment\": \"comment1\",\"rule_disabled\": false," +
            "\"rule_services\": [{\"service\": {\"svc_id\": 1,\"svc_name\": \"TestService1\",\"svc_uid\": \"\",\"svc_port\": 443,\"svc_port_end\": 443,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 6,\"name\": \"TCP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}}]," +
            "\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_src_neg\": false,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"srczn\"}," +
            "\"rule_froms\": [{\"object\": {\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}},{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_dst_neg\": false,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"dstzn\"},\"rule_tos\": [{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"ip_range\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_action\": \"accept\",\"rule_track\": \"none\",\"section_header\": \"\",\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": \"2022-04-19T00:00:00\",\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
            "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 1,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false},\"new\": {\"rule_id\": 0,\"rule_uid\": \"\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"TestRule1\",\"rule_comment\": \"new comment\",\"rule_disabled\": false," +
            "\"rule_services\": [{\"service\": {\"svc_id\": 1,\"svc_name\": \"TestService1\",\"svc_uid\": \"\",\"svc_port\": 443,\"svc_port_end\": 443,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 6,\"name\": \"TCP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}}]," +
            "\"rule_svc_neg\": true,\"rule_svc\": \"\",\"rule_src_neg\": false,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"srczn\"},\"rule_froms\": [{\"object\": {\"obj_id\": 5,\"obj_name\": \"TestIp1Changed\",\"obj_ip\": \"2.3.4.5/32\",\"obj_ip_end\": \"2.3.4.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"host\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}},{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_dst_neg\": false,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"dstzn\"},\"rule_tos\": [{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"ip_range\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}},{\"object\": {\"obj_id\": 4,\"obj_name\": \"TestIpNew\",\"obj_ip\": \"10.0.6.0/32\",\"obj_ip_end\": \"10.0.6.255/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 0,\"user_uid\": \"\",\"user_name\": \"\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_action\": \"accept\",\"rule_track\": \"none\",\"section_header\": \"\",\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": \"2022-04-19T00:00:00\",\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
            "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 1,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false},\"DeviceName\": \"\"}," +
            "{\"import\": {\"time\": \"2023-04-05T12:00:00\"},\"change_action\": \"C\",\"old\": {\"rule_id\": 0,\"rule_uid\": \"uid2:123\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"TestRule2\",\"rule_comment\": \"comment2\",\"rule_disabled\": false," +
            "\"rule_services\": [{\"service\": {\"svc_id\": 2,\"svc_name\": \"TestService2\",\"svc_uid\": \"\",\"svc_port\": 6666,\"svc_port_end\": 7777,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 17,\"name\": \"UDP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}}]," +
            "\"rule_svc_neg\": true,\"rule_svc\": \"\",\"rule_src_neg\": true,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"\"}," +
            "\"rule_froms\": [{\"object\": {\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}},{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_dst_neg\": true,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"rule_tos\": [{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"ip_range\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 2,\"user_uid\": \"\",\"user_name\": \"TestUser2\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"group\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_action\": \"deny\",\"rule_track\": \"none\",\"section_header\": \"\",\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": null,\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
            "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 2,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false},\"new\": {\"rule_id\": 0,\"rule_uid\": \"uid2:123\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"TestRule2\",\"rule_comment\": \"comment2\",\"rule_disabled\": true," +
            "\"rule_services\": [{\"service\": {\"svc_id\": 2,\"svc_name\": \"TestService2\",\"svc_uid\": \"\",\"svc_port\": 6666,\"svc_port_end\": 7777,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 17,\"name\": \"UDP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}}]," +
            "\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_src_neg\": true,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"\"}," +
            "\"rule_froms\": [{\"object\": {\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}},{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_dst_neg\": false,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"rule_tos\": [{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"ip_range\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 2,\"user_uid\": \"\",\"user_name\": \"TestUser2\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"group\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_action\": \"deny\",\"rule_track\": \"none\",\"section_header\": \"\",\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": null,\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
            "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 2,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false},\"DeviceName\": \"\"}," +
            "{\"import\": {\"time\": \"2023-04-05T12:00:00\"},\"change_action\": \"D\",\"old\": {\"rule_id\": 0,\"rule_uid\": \"uid2:123\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"TestRule2\",\"rule_comment\": \"comment2\",\"rule_disabled\": false," +
            "\"rule_services\": [{\"service\": {\"svc_id\": 2,\"svc_name\": \"TestService2\",\"svc_uid\": \"\",\"svc_port\": 6666,\"svc_port_end\": 7777,\"svc_source_port\": null,\"svc_source_port_end\": null,\"svc_code\": \"\",\"svc_timeout\": null,\"svc_typ_id\": null,\"active\": false,\"svc_create\": 0,\"svc_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"svc_last_seen\": 0,\"service_type\": {\"name\": \"\"},\"svc_comment\": \"\",\"svc_color_id\": null,\"ip_proto_id\": null,\"protocol_name\": {\"id\": 17,\"name\": \"UDP\"},\"svc_member_names\": \"\",\"svc_member_refs\": \"\",\"svcgrps\": [],\"svcgrp_flats\": []}}]," +
            "\"rule_svc_neg\": true,\"rule_svc\": \"\",\"rule_src_neg\": true,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"\"}," +
            "\"rule_froms\": [{\"object\": {\"obj_id\": 1,\"obj_name\": \"TestIp1\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.4/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}},{\"object\": {\"obj_id\": 2,\"obj_name\": \"TestIp2\",\"obj_ip\": \"127.0.0.1/32\",\"obj_ip_end\": \"127.0.0.1/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"network\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 1,\"user_uid\": \"\",\"user_name\": \"TestUser1\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_dst_neg\": true,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"rule_tos\": [{\"object\": {\"obj_id\": 3,\"obj_name\": \"TestIpRange\",\"obj_ip\": \"1.2.3.4/32\",\"obj_ip_end\": \"1.2.3.5/32\",\"obj_uid\": \"\",\"zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"active\": false,\"obj_create\": 0,\"obj_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"obj_last_seen\": 0,\"type\": {\"name\": \"ip_range\"},\"obj_comment\": \"\",\"obj_member_names\": \"\",\"obj_member_refs\": \"\",\"objgrps\": [],\"objgrp_flats\": []}," +
            "\"usr\": {\"user_id\": 2,\"user_uid\": \"\",\"user_name\": \"TestUser2\",\"user_comment\": \"\",\"user_lastname\": \"\",\"user_firstname\": \"\",\"usr_typ_id\": 0,\"type\": {\"usr_typ_name\": \"group\"},\"user_create\": 0,\"user_create_time\": {\"time\": \"0001-01-01T00:00:00\"},\"user_last_seen\": 0,\"user_member_names\": \"\",\"user_member_refs\": \"\",\"usergrps\": [],\"usergrp_flats\": []}}]," +
            "\"rule_action\": \"deny\",\"rule_track\": \"none\",\"section_header\": \"\",\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": null,\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
            "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 2,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false},\"new\": {\"rule_id\": 0,\"rule_uid\": \"\",\"mgm_id\": 0,\"rule_num_numeric\": 0,\"rule_name\": \"\",\"rule_comment\": \"\",\"rule_disabled\": false," +
            "\"rule_services\": [],\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_src_neg\": false,\"rule_src\": \"\",\"src_zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"dst_zone\": {\"zone_id\": 0,\"zone_name\": \"\"},\"rule_tos\": [],\"rule_action\": \"\",\"rule_track\": \"\",\"section_header\": \"\"," +
            "\"rule_metadatum\": {\"rule_metadata_id\": 0,\"rule_created\": null,\"rule_last_modified\": null,\"rule_first_hit\": null,\"rule_last_hit\": null,\"rule_last_certified\": null,\"rule_last_certifier_dn\": \"\",\"rule_to_be_removed\": false,\"rule_decert_date\": null,\"rule_recertification_comment\": \"\",\"recertification\": [],\"recert_history\": [],\"dev_id\": 0,\"rule_uid\": \"\",\"NextRecert\": \"0001-01-01T00:00:00\",\"LastCertifierName\": \"\",\"Recert\": false,\"Style\": \"\"}," +
            "\"translate\": {\"rule_svc_neg\": false,\"rule_svc\": \"\",\"rule_services\": [],\"rule_src_neg\": false,\"rule_src\": \"\",\"rule_froms\": [],\"rule_dst_neg\": false,\"rule_dst\": \"\",\"rule_tos\": []}," +
            "\"owner_name\": \"\",\"owner_id\": null,\"matches\": \"\",\"dev_id\": 0,\"rule_custom_fields\": \"\",\"DisplayOrderNumber\": 0,\"Certified\": false,\"DeviceName\": \"\",\"DisregardedFroms\": [],\"DisregardedTos\": [],\"DisregardedServices\": [],\"ShowDisregarded\": false},\"DeviceName\": \"\"}],\"rules_aggregate\": {\"aggregate\": {\"count\": 0}}}]," +
            "\"import\": {\"aggregate\": {\"max\": {\"id\": null}}},\"RelevantImportId\": null," +
            "\"networkObjects\": [],\"serviceObjects\": [],\"userObjects\": [],\"reportNetworkObjects\": [],\"reportServiceObjects\": [],\"reportUserObjects\": [],\"ReportedRuleIds\": [],\"ReportedNetworkServiceIds\": [],\"objects_aggregate\": {\"aggregate\": {\"count\": 0}}," +
            "\"services_aggregate\": {\"aggregate\": {\"count\": 0}},\"usrs_aggregate\": {\"aggregate\": {\"count\": 0}},\"rules_aggregate\": {\"aggregate\": {\"count\": 0}}," +
            "\"Ignore\": false}]";
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToJson(), false, true)));
        }

        [Test]
        public void ResolvedChangesGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting resolved changes report json generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChanges, new TimeFilter())
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedJsonResult =
            "{\"report type\": \"Changes Report (resolved)\",\"report generation date\": \"Z (UTC)\",\"device filter\": \"TestMgt [TestDev]\",\"other filters\": \"TestFilter\",\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"gateways\": [{\"TestDev\": {\"rule changes\": [" +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule added\",\"name\": \"TestRule1\"," +
            "\"source zone\": \"srczn\",\"source negated\": false,\"source\": [\"TestIp1 (1.2.3.4/32)\",\"TestIp2 (127.0.0.1/32)\"]," +
            "\"destination zone\": \"dstzn\",\"destination negated\": false,\"destination\": [\"TestIpRange (1.2.3.4-1.2.3.5)\"]," +
            "\"service negated\": false,\"service\": [\"TestService1 (443/TCP)\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule1\"," +
            "\"source zone\": \"srczn\",\"source negated\": false,\"source\": [\"TestIp2 (127.0.0.1/32)\",\"deleted: TestIp1 (1.2.3.4/32)\",\"added: TestIp1Changed (2.3.4.5)\"]," +
            "\"destination zone\": \"dstzn\",\"destination negated\": false,\"destination\": [\"TestIpRange (1.2.3.4-1.2.3.5)\",\"added: TestIpNew (10.0.6.0/24)\"]," +
            "\"service negated\": \"deleted: false, added: true\",\"service\": [\"TestService1 (443/TCP)\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"deleted: uid1\",\"comment\": \"deleted: comment1, added: new comment\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule2\"," +
            "\"source zone\": \"\",\"source negated\": true,\"source\": [\"TestUser1@TestIp1 (1.2.3.4/32)\",\"TestUser1@TestIp2 (127.0.0.1/32)\"]," +
            "\"destination zone\": \"\",\"destination negated\": \"deleted: true, added: false\",\"destination\": [\"TestUser2@TestIpRange (1.2.3.4-1.2.3.5)\"]," +
            "\"service negated\": \"deleted: true, added: false\",\"service\": [\"TestService2 (6666-7777/UDP)\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": \"deleted: false, added: true\",\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule deleted\",\"name\": \"TestRule2\"," +
            "\"source zone\": \"\",\"source negated\": true,\"source\": [\"TestUser1@TestIp1 (1.2.3.4/32)\",\"TestUser1@TestIp2 (127.0.0.1/32)\"]," +
            "\"destination zone\": \"\",\"destination negated\": true,\"destination\": [\"TestUser2@TestIpRange (1.2.3.4-1.2.3.5)\"]," +
            "\"service negated\": true,\"service\": [\"TestService2 (6666-7777/UDP)\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]}}]}}]}";
            // Log.WriteInfo("Test Log", removeLinebreaks((removeGenDate(reportChanges.ExportToJson(), false, true))));
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToJson(), false, true)));
        }

        [Test]
        public void ResolvedChangesTechGenerateJson()
        {
            Log.WriteInfo("Test Log", "starting resolved changes report json generation");
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChangesTech, new TimeFilter())
            {
                ReportData = ConstructChangeReport(true)
            };

            string expectedJsonResult =
            "{\"report type\": \"Changes Report (technical)\",\"report generation date\": \"Z (UTC)\",\"device filter\": \"TestMgt [TestDev]\",\"other filters\": \"TestFilter\",\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",\"data protection level\": \"For internal use only\"," +
            "\"managements\": [{\"TestMgt\": {\"gateways\": [{\"TestDev\": {\"rule changes\": [" +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule added\",\"name\": \"TestRule1\"," +
            "\"source zone\": \"srczn\",\"source negated\": false,\"source\": [\"1.2.3.4/32\",\"127.0.0.1/32\"]," +
            "\"destination zone\": \"dstzn\",\"destination negated\": false,\"destination\": [\"1.2.3.4-1.2.3.5\"]," +
            "\"service negated\": false,\"service\": [\"443/TCP\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid1\",\"comment\": \"comment1\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule1\"," +
            "\"source zone\": \"srczn\",\"source negated\": false,\"source\": [\"127.0.0.1/32\",\"deleted: 1.2.3.4/32\",\"added: 2.3.4.5\"]," +
            "\"destination zone\": \"dstzn\",\"destination negated\": false,\"destination\": [\"1.2.3.4-1.2.3.5\",\"added: 10.0.6.0/24\"]," +
            "\"service negated\": \"deleted: false, added: true\",\"service\": [\"443/TCP\"],\"action\": \"accept\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"deleted: uid1\",\"comment\": \"deleted: comment1, added: new comment\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule modified\",\"name\": \"TestRule2\"," +
            "\"source zone\": \"\",\"source negated\": true,\"source\": [\"TestUser1@1.2.3.4/32\",\"TestUser1@127.0.0.1/32\"]," +
            "\"destination zone\": \"\",\"destination negated\": \"deleted: true, added: false\",\"destination\": [\"TestUser2@1.2.3.4-1.2.3.5\"]," +
            "\"service negated\": \"deleted: true, added: false\",\"service\": [\"6666-7777/UDP\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": \"deleted: false, added: true\",\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}," +
            "{\"change time\": \"05.04.2023 12:00:00\",\"change action\": \"Rule deleted\",\"name\": \"TestRule2\"," +
            "\"source zone\": \"\",\"source negated\": true,\"source\": [\"TestUser1@1.2.3.4/32\",\"TestUser1@127.0.0.1/32\"]," +
            "\"destination zone\": \"\",\"destination negated\": true,\"destination\": [\"TestUser2@1.2.3.4-1.2.3.5\"]," +
            "\"service negated\": true,\"service\": [\"6666-7777/UDP\"],\"action\": \"deny\",\"tracking\": \"none\",\"disabled\": false,\"rule uid\": \"uid2:123\",\"comment\": \"comment2\"}]}}]}}]}";
            ClassicAssert.AreEqual(expectedJsonResult, RemoveLinebreaks(RemoveGenDate(reportChanges.ExportToJson(), false, true)));
        }


        private static NetworkLocation[] InitFroms(bool resolved, bool user = false)
        {
            if (resolved)
            {
                return [ new NetworkLocation(user ? TestUser1 : new NetworkUser(), new NetworkObject(){ ObjectGroupFlats =
                [
                    new(){ Object = TestIp1 },
                    new(){ Object = TestIp2 }
                ]})];
            }
            else
            {
                return
                [
                    new(user ? TestUser1 : new NetworkUser(), TestIp1),
                    new(user ? TestUser1 : new NetworkUser(), TestIp2)
                ];
            }
        }

        private static NetworkLocation[] InitTos(bool resolved, bool user = false)
        {
            if (resolved)
            {
                return [ new NetworkLocation(user ? TestUser2 : new NetworkUser(), new NetworkObject(){ ObjectGroupFlats =
                [
                    new(){ Object = TestIpRange }
                ]})];
            }
            else
            {
                return
                [
                    new(user ? TestUser2 : new NetworkUser(), TestIpRange),
                ];
            }
        }

        private static ServiceWrapper[] InitServices(NetworkService service, bool resolved)
        {
            if (resolved)
            {
                return [new ServiceWrapper(){ Content = new NetworkService(){ServiceGroupFlats =
                [
                    new GroupFlat<NetworkService>(){ Object = service }
                ]}}];
            }
            else
            {
                return
                [
                    new(){ Content = service },
                ];
            }
        }

        private static Rule InitRule1(bool resolved)
        {
            return new Rule()
            {
                Name = "TestRule1",
                Action = "accept",
                Comment = "comment1",
                Disabled = false,
                DisplayOrderNumber = 1,
                Track = "none",
                Uid = "uid1",
                SourceZone = new NetworkZone() { Name = "srczn" },
                SourceNegated = false,
                Froms = InitFroms(resolved),
                DestinationZone = new NetworkZone() { Name = "dstzn" },
                DestinationNegated = false,
                Tos = InitTos(resolved),
                ServiceNegated = false,
                Services = InitServices(TestService1, resolved),
                Metadata = new RuleMetadata() { LastHit = new DateTime(2022, 04, 19) }
            };
        }

        private static Rule InitRule2(bool resolved)
        {
            return new Rule()
            {
                Name = "TestRule2",
                Action = "deny",
                Comment = "comment2",
                Disabled = false,
                DisplayOrderNumber = 2,
                Track = "none",
                Uid = "uid2:123",
                SourceNegated = true,
                Froms = InitFroms(resolved, true),
                DestinationNegated = true,
                Tos = InitTos(resolved, true),
                ServiceNegated = true,
                Services = InitServices(TestService2, resolved)
            };
        }

        private static ReportData ConstructRuleReport(bool resolved)
        {
            Rule1 = InitRule1(resolved);
            Rule2 = InitRule2(resolved);
            return new ReportData()
            {
                ManagementData =
                [
                    new ()
                    {
                        Name = "TestMgt",
                        ReportObjects = [TestIp1, TestIp2, TestIpRange],
                        ReportServices = [TestService1, TestService2],
                        ReportUsers = [TestUser1, TestUser2],
                        Devices =
                        [
                            new ()
                            {
                                Name = "TestDev",
                                Rules = [Rule1, Rule2]
                            }
                        ]
                    }
                ]
            };
        }

        private static ReportData ConstructRecertReport()
        {
            RecertRule1 = InitRule1(false);
            RecertRule1.Metadata.RuleRecertification =
            [
                new ()
                {
                    NextRecertDate  = DateTime.Now.AddDays(5),
                    FwoOwner = new FwoOwner(){ Name = "TestOwner1" },
                    IpMatch = TestIp1.Name
                },
                new ()
                {
                    NextRecertDate  = DateTime.Now.AddDays(-5),
                    FwoOwner = new FwoOwner(){ Name = "TestOwner2" },
                    IpMatch = TestIp2.Name
                }
            ];
            RecertRule2 = InitRule2(false);
            RecertRule2.Metadata.RuleRecertification =
            [
                new ()
                {
                    NextRecertDate  = DateTime.Now,
                    FwoOwner = new FwoOwner(){ Name = "TestOwner1" },
                    IpMatch = TestIpRange.Name
                }
            ];
            return new ReportData()
            {
                ManagementData =
                [
                    new ()
                    {
                        Name = "TestMgt",
                        ReportObjects = [TestIp1, TestIp2, TestIpRange],
                        ReportServices = [TestService1, TestService2],
                        ReportUsers = [TestUser1, TestUser2],
                        Devices =
                        [
                            new ()
                            {
                                Name = "TestDev",
                                Rules = [RecertRule1, RecertRule2]
                            }
                        ]
                    }
                ]
            };
        }

        private static ReportData ConstructNatRuleReport()
        {
            NatRule = InitRule1(false);
            NatRule.NatData = new NatData()
            {
                TranslatedSourceNegated = false,
                TranslatedFroms =
                [
                    new (TestUser2, TestIp1Changed)
                ],
                TranslatedDestinationNegated = true,
                TranslatedTos =
                [
                    new (new NetworkUser(), TestIp1Changed),
                    new (new NetworkUser(), TestIpNew)
                ],
                TranslatedServiceNegated = false,
                TranslatedServices =
                [
                    new (){ Content = TestService1 },
                    new (){ Content = TestService2 }
                ]
            };
            return new ReportData()
            {
                ManagementData =
                [
                    new ()
                    {
                        Name = "TestMgt",
                        ReportObjects = [TestIp1, TestIp2, TestIpRange, TestIpNew, TestIp1Changed],
                        ReportServices = [TestService1, TestService2],
                        ReportUsers = [TestUser2],
                        Devices =
                        [
                            new (){ Name = "TestDev", Rules = [NatRule]}
                        ]
                    }
                ]
            };
        }

        private static ReportData ConstructChangeReport(bool resolved)
        {
            Rule1 = InitRule1(resolved);
            Rule1Changed = InitRule1(resolved);
            Rule2 = InitRule2(resolved);
            Rule2Changed = InitRule2(resolved);
            if (resolved)
            {
                Rule1Changed.Froms[0].Object.ObjectGroupFlats[0].Object = TestIp1Changed;
                Rule1Changed.Tos = [new (new NetworkUser(), new NetworkObject(){ObjectGroupFlats =
                [
                    new (){ Object = TestIpRange },
                    new (){ Object = TestIpNew }
                ]})];
            }
            else
            {
                Rule1Changed.Froms[0].Object = TestIp1Changed;
                Rule1Changed.Tos =
                [
                    new (new NetworkUser(), TestIpRange),
                    new (new NetworkUser(), TestIpNew)
                ];
            }
            Rule1Changed.Uid = "";
            Rule1Changed.ServiceNegated = true;
            Rule1Changed.Comment = "new comment";

            Rule2Changed.DestinationNegated = false;
            Rule2Changed.ServiceNegated = false;
            Rule2Changed.Disabled = true;

            RuleChange ruleChange1 = new()
            {
                ChangeAction = 'I',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                NewRule = Rule1
            };
            RuleChange ruleChange2 = new()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldRule = Rule1,
                NewRule = Rule1Changed
            };
            RuleChange ruleChange3 = new()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldRule = Rule2,
                NewRule = Rule2Changed
            };
            RuleChange ruleChange4 = new()
            {
                ChangeAction = 'D',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldRule = Rule2
            };
            return new ReportData()
            {
                ManagementData =
                [
                    new ()
                    {
                        Name = "TestMgt",
                        Devices =
                        [
                            new ()
                            {
                                Name = "TestDev",
                                RuleChanges = [ruleChange1, ruleChange2, ruleChange3, ruleChange4]
                            }
                        ]
                    }
                ]
            };
        }

        private static async Task<ReportData> ConstructAppRulesReport()
        {
            ReportData reportData = ConstructRuleReport(false);
            ModellingVarianceAnalysisTestApiConn apiConnection = new();
            reportData.ManagementData = await ReportAppRules.PrepareAppRulesReport(reportData.ManagementData, new ModellingFilter(), apiConnection, 1);
            return reportData;
        }

        private static ReportData ConstructConnectionReport()
        {
            ModellingAppServer AppServer1 = new() { Id = 11, Number = 1, Name = "AppServer1", Ip = "1.0.0.0" };
            ModellingAppServer AppServer2 = new() { Id = 12, Number = 2, Name = "AppServer2", Ip = "2.0.0.0" };
            ModellingAppRole AppRole1 = new() { Id = 21, Number = 3, Name = "AppRole1", IdString = "AR1", Comment = "CommAR1", AppServers = [new() { Content = AppServer1 }] };
            ModellingService Service1 = new() { Id = 31, Number = 1, Name = "Service1", Port = 1234, Protocol = new() { Id = 6, Name = "TCP" } };
            ModellingService Service2 = new() { Id = 32, Number = 2, Name = "Service2", Port = 2345, Protocol = new() { Id = 17, Name = "UDP" } };
            ModellingServiceGroup ServiceGroup1 = new() { Id = 41, Number = 3, Name = "ServiceGroup1", Comment = "CommSG1", Services = [new() { Content = Service1 }] };
            ModellingConnection Conn1 = new()
            {
                Id = 101,
                Name = "Conn1",
                SourceAppServers = [new() { Content = AppServer1 }],
                DestinationAppRoles = [new() { Content = AppRole1 }],
                Services = [new() { Content = Service1 }],
                ServiceGroups = [new() { Content = ServiceGroup1 }]
            };
            ModellingConnection Inter2 = new()
            {
                Id = 102,
                Name = "Inter2",
                DestinationAppServers = [new() { Content = AppServer2 }],
                DestinationAppRoles = [new() { Content = new() { Name = "noRole" } }],
                Services = [new() { Content = Service2 }],
                ServiceGroups = [new() { }]
            };
            ModellingConnection ComSvc3 = new()
            {
                Id = 103,
                Name = "ComSvc3",
                App = new() { Name = "App1" },
                SourceAppServers = [new() { Content = AppServer1 }],
                DestinationAppServers = [new() { Content = AppServer2 }],
                Services = [new() { Content = Service2 }],
                ServiceGroups = [new() { }]
            };

            ReportData reportData = new()
            {
                OwnerData =
                [
                    new ()
                    {
                        Name = "TestOwner",
                        Connections = [Conn1, Inter2, ComSvc3],
                        RegularConnections = [Conn1],
                        Interfaces = [Inter2],
                        CommonServices = [ComSvc3],
                    }
                ],
                GlobalComSvc = [new() { GlobalComSvcs = [ComSvc3] }]
            };
            reportData.OwnerData[0].PrepareObjectData(true);
            return reportData;
        }

        private static ReportData ConstructVarianceReport()
        {
            ModellingAppServer AppServer1 = new() { Id = 11, Number = 1, Name = "AppServer1", Ip = "1.0.0.0" };
            ModellingAppServer AppServer2 = new() { Id = 12, Number = 2, Name = "AppServer2", Ip = "2.0.0.0" };
            ModellingAppRole AppRole1 = new() { Id = 21, Number = 3, Name = "AppRole1", IdString = "AR1", Comment = "CommAR1", AppServers = [new() { Content = AppServer1 }] };
            ModellingAppRole AppRole2 = new() { Id = 22, Number = 4, Name = "AppRole2", IdString = "AR2", Comment = "CommAR2", AppServers = [new() { Content = AppServer2 }] };
            NetworkSubnet Subnet1 = new(){ Id = 1, Name = "Net1", Ip = "1.0.0.0" };
            ModellingNetworkArea Area1 = new() { Id = 51, Number = 5, Name = "Area50", IdString = "NA50", IpData = [new() { Content = Subnet1 }] };
            Dictionary<int, List<ModellingAppRole>> MissAR = new() { [0] = [ AppRole1 ] };
            Dictionary<int, List<ModellingAppRole>> DiffAR = new() { [0] = [ AppRole2 ] };
            ModellingService Service1 = new() { Id = 31, Number = 1, Name = "Service1", Port = 1234, Protocol = new() { Id = 6, Name = "TCP" } };
            ModellingServiceGroup ServiceGroup1 = new() { Id = 41, Number = 3, Name = "ServiceGroup1", Comment = "CommSG1", Services = [new() { Content = Service1 }] };
            ModellingConnection Conn1 = new()
            {
                Id = 101,
                Name = "Conn1",
                SourceAppServers = [new() { Content = AppServer1 }],
                SourceAppRoles = [new() { Content = AppRole1 }],
                SourceAreas = [new() { Content = Area1 }],
                DestinationAppRoles = [new() { Content = AppRole2 }],
                Services = [new() { Content = Service1 }],
                ServiceGroups = [new() { Content = ServiceGroup1 }]
            };
            ModellingConnection Conn2 = new()
            {
                Id = 102,
                Name = "Conn2",
                SourceAppServers = [new() { Content = AppServer1 }],
                DestinationAppRoles = [new() { Content = AppRole2 }],
                Services = [new() { Content = Service1 }],
            };
            Rule1 = InitRule1(true);

            ReportData reportData = new()
            {
                OwnerData =
                [
                    new ()
                    {
                        Name = "TestOwner",
                        Connections = [Conn1],
                        RegularConnections = [Conn1],
                        MissingAppRoles = MissAR,
                        DifferingAppRoles = DiffAR,
                        RuleDifferences = [],
                        ModelledConnectionsCount = 2,
                        AppRoleStats = new() 
                        {
                            ModelledAppRolesCount = 2,
                            AppRolesOk = 0,
                            AppRolesMissingCount = 1,
                            AppRolesDifferenceCount = 1
                        }
                    }
                ]
            };
            reportData.OwnerData[0].RuleDifferences.Add(new(){ModelledConnection = Conn2, ImplementedRules = [Rule1]});
            reportData.OwnerData[0].PrepareObjectData(true);
            return reportData;
        }

        private static string RemoveGenDate(string exportString, bool html = false, bool json = false)
        {
            string Quote = json ? "\"" : "";
            string dateText = html ? "<p>Generated on: " : "report generation date" + Quote + ": " + Quote;
            int startGenTime = exportString.IndexOf(dateText);
            if (startGenTime > 0)
            {
                return exportString.Remove(startGenTime + dateText.Length, 19);
            }
            return exportString;
        }

        private static string RemoveLinebreaks(string exportString)
        {
            while (exportString.Contains("\n "))
            {
                exportString = exportString.Replace("\n ", "\n");
            }
            while (exportString.Contains(" \n"))
            {
                exportString = exportString.Replace(" \n", "\n");
            }
            while (exportString.Contains(" \r"))
            {
                exportString = exportString.Replace(" \r", "\r");
            }
            exportString = exportString.Replace("\r", "");
            return exportString.Replace("\n", "");
        }
    }
}
