using FWO.Api.Data;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using RestSharp.Serializers;
using FWO.Logging;

namespace FWO.Tufin.SecureChange
{
	public class ExternalTicket
	{
		[JsonProperty("ticketText"), JsonPropertyName("ticketText")]
		public string TicketText { get; set; } = "";

		protected List<string> TicketTasks = [];
		protected ExternalTicketSystem TicketSystem = new();


		public ExternalTicket(){}

		public virtual void CreateRequestString(List<WfReqTask> tasks)
		{}

		public virtual string GetTaskTypeAsString(WfReqTask task)
		{
			return "";
		}

		public async Task<RestResponse<int>> CreateExternalTicket(ExternalTicketSystem ticketSystem)
		{
			// build API call
			RestRequest request = new("tickets.json", Method.Post);
			request.AddJsonBody(TicketText);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("Authorization", ticketSystem.Authorization);
			RestClientOptions restClientOptions = new();
			restClientOptions.RemoteCertificateValidationCallback += (_, _, _, _) => true;
			restClientOptions.BaseUrl = new Uri(ticketSystem.Url);
			RestClient restClient = new(restClientOptions, null, ConfigureRestClientSerialization);

			// Debugging SecureChange API call
			DebugApiCall(request, restClient);

			// send API call
			return await restClient.ExecuteAsync<int>(request);
		}

		private void ConfigureRestClientSerialization(SerializerConfig config)
		{
			JsonNetSerializer serializer = new (); // Case insensivitive is enabled by default
			config.UseSerializer(() => serializer);
		}

		private static void DebugApiCall(RestRequest request, RestClient restClient)
		{
			string headers = "";
			string body = "";
			foreach (Parameter p in request.Parameters)
			{
				if (p.Name == "")
				{
					body = $"data: '{p.Value}'";
				}
				else
				{
					if (p.Name != "Authorization") // avoid logging of credentials
						headers += $"header: '{p.Name}: {p.Value}' ";
				}
			}
			Log.WriteDebug("API", $"Sending API Call to SecureChange:\nrequest: {request.Method}, url: {restClient.Options.BaseUrl}, {body}, {headers} ");
		}
	}
}
