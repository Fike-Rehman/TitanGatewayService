using System.Net.Http.Headers;

namespace TitanGatewayService.Devices
{
    public class MirandaClient : IDeviceClient
    {
        public string Name { get; }

        public string BaseUrl { get; }

        public string Location { get; }

        public MirandaClient(string name, string location, string baseUrl)
        {
            Name = name;
            Location = location;
            BaseUrl = baseUrl;
        }

        public async Task<string> PingAsync()
        {
            var pingResponse = "";

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                client.Timeout = TimeSpan.FromMilliseconds(10000);

                try
                {
                    var response = await client.GetAsync("/ping");

                    if (response.IsSuccessStatusCode)
                    {
                        pingResponse = "OK";
                    }

                }
                catch (Exception x)
                {
                    // the request takes longer than 10 secs, it is timed out
                    pingResponse = x.Message;
                }

                return pingResponse;
            }
        }
    }
}
