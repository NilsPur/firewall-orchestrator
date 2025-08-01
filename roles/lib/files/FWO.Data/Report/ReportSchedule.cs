using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Data.Report
{
    public class ReportSchedule
    {
        [JsonProperty("report_schedule_id"), JsonPropertyName("report_schedule_id")]
        public int Id { get; set; }

        [JsonProperty("report_schedule_name"), JsonPropertyName("report_schedule_name")]
        public string Name { get; set; } = "";

        [JsonProperty("report_schedule_owner_user"), JsonPropertyName("report_schedule_owner_user")]
        public UiUser ScheduleOwningUser { get; set; } = new ();

        [JsonProperty("report_schedule_start_time"), JsonPropertyName("report_schedule_start_time")]
        public DateTime StartTime { get; set; } = DateTime.Now.AddSeconds(-DateTime.Now.Second);

        [JsonProperty("report_schedule_repeat"), JsonPropertyName("report_schedule_repeat")]
        public int RepeatOffset { get; set; } = 1;

        [JsonProperty("report_schedule_every"), JsonPropertyName("report_schedule_every")]
        public SchedulerInterval RepeatInterval { get; set; }

        [JsonProperty("report_schedule_template"), JsonPropertyName("report_schedule_template")]
        public ReportTemplate Template { get; set; } = new ();

        [JsonProperty("report_schedule_formats"), JsonPropertyName("report_schedule_formats")]
        public List<FileFormat> OutputFormat { get; set; } = [];

        [JsonProperty("report_schedule_active"), JsonPropertyName("report_schedule_active")]
        public bool Active { get; set; } = true;

        [JsonProperty("report_schedule_counter"), JsonPropertyName("report_schedule_counter")]
        public int Counter { get; set; }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            return shortened;
        }
    }
}
