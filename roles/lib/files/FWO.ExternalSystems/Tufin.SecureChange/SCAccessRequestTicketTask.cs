using FWO.Data;
using FWO.Data.Workflow;
using FWO.Data.Modelling;
using FWO.Basics;

namespace FWO.ExternalSystems.Tufin.SecureChange
{
	public class SCAccessRequestTicketTask : SCTicketTask
	{
		public SCAccessRequestTicketTask(WfReqTask reqTask, List<IpProtocol> ipProtos, ModellingNamingConvention? namingConvention = null) : base(reqTask, ipProtos, namingConvention)
		{}

		/// {
		/// 	"order": "@@ORDERNAME@@",
		/// 	"verifier_result": {
		/// 		"status": "not run"
		/// 	},
		/// 	"use_topology": true,
		/// 	"targets": {
		/// 		"target": {
		/// 			"@type": "ANY"
		/// 		}
		/// 	},
		///  	"action": @@ACTION@@,
		///		"sources": {
		/// 		"source": @@SOURCES@@
		/// 	},
		/// 	"destinations": {
		/// 		"destination": @@DESTINATIONS@@
		/// 	},
		/// 	"services": {
		/// 		"service": @@SERVICES@@
		/// 	},
		/// 	"labels": "",
		///  	"comment": "@@TASKCOMMENT@@
		/// }
		public override void FillTaskText(ExternalTicketTemplate template)
		{
			ExtMgtData extMgt = ReqTask.OnManagement != null && ReqTask.OnManagement?.ExtMgtData != null ?
				System.Text.Json.JsonSerializer.Deserialize<ExtMgtData>(ReqTask.OnManagement?.ExtMgtData ?? "{}") : new();
			TaskText = template.TasksTemplate
				.Replace(Placeholder.ORDERNAME, "AR"+ ReqTask.TaskNumber.ToString())
				.Replace(Placeholder.TASKCOMMENT, ReqTask.GetFirstCommentText())
				.Replace(Placeholder.ACTION, MapActionType(ReqTask))
				.Replace(Placeholder.SOURCES, ConvertNetworkElems(template, UseModelled() ? ElemFieldType.modelled_source : ElemFieldType.source, extMgt.ExtName))
				.Replace(Placeholder.DESTINATIONS, ConvertNetworkElems(template, UseModelled() ? ElemFieldType.modelled_destination : ElemFieldType.destination, extMgt.ExtName))
				.Replace(Placeholder.SERVICES, ConvertServiceElems(template));
		}

		private static string MapActionType(WfReqTask reqTask)
		{
			return reqTask.TaskType switch
            {
                nameof(WfTaskType.access) => SCActionType.Accept,
                nameof(WfTaskType.rule_modify) => SCActionType.Accept,
                nameof(WfTaskType.rule_delete) => SCActionType.Remove,
                _ => "",
            };
		}

		private string ConvertNetworkElems(ExternalTicketTemplate template, ElemFieldType fieldType, string? mgtName)
		{
			List<NwObjectElement> nwObjects = ReqTask.GetNwObjectElements(fieldType);
			List<string> convertedObjects = [];
			foreach(var nwObj in nwObjects)
			{
				if(nwObj.GroupName != "")
				{
					if(convertedObjects.FirstOrDefault(o => o == nwObj.GroupName) == null)
					{
						convertedObjects.Add(FillNwObjGroupTemplate(template, nwObj.GroupName, mgtName ?? ""));
					}
				}
				else
				{
					convertedObjects.Add(FillIpTemplate(template, nwObj.IpString));
				}
			}
			return "[" + string.Join(",", convertedObjects) + "]";
		}

		private string ConvertServiceElems(ExternalTicketTemplate template)
		{
			List<NwServiceElement> nwServiceElements = ReqTask.GetServiceElements();
			List<string> convertedObjects = [];
			foreach (var svc in nwServiceElements)
			{
				if (svc.ProtoId == 1) // ICMP
				{
					convertedObjects.Add(FillIcmpTemplate(template, svc.Name ?? ""));
				}
				else if (svc.ProtoId == 6 || svc.ProtoId == 17) // TCP, UDP
				{
					convertedObjects.Add(FillServiceTemplate(template, IpProtos.FirstOrDefault(x => x.Id == svc.ProtoId)?.Name ?? svc.ProtoId.ToString(), DisplayPortRange(svc.Port, svc.PortEnd), svc.Name ?? ""));
				}
				else
				{
					convertedObjects.Add(FillIpProtocolTemplate(template, IpProtos.FirstOrDefault(x => x.Id == svc.ProtoId)?.Name ?? svc.ProtoId.ToString(), svc.ProtoId.ToString(), svc.Name ?? ""));
				}
			}
			return "[" + string.Join(",", convertedObjects) + "]";
		}

		public static string DisplayPortRange(int port, int? portEnd)
		{
			return portEnd == null || portEnd == 0 || port == portEnd ? $"{port}" : $"{port}-{portEnd}";
		}

		private bool UseModelled()
		{
			return ReqTask.TaskType == WfTaskType.rule_delete.ToString() && ReqTask.Elements.Where(e => e.Field == ElemFieldType.modelled_source.ToString()).ToList().Count > 0;
		}
	}
}
