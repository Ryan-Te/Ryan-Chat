using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading;
//using System.Text.Json;
//using System.Text.Json.Serialization;

namespace Ryan_Chat
{
	class HTTPResponse
	{
		public string Success;
		public string Data;
		public Newtonsoft.Json.Linq.JObject JSON;
	}

	class Program
	{
		static HTTPResponse SendRequest(string URL, string Method, string JSON)
		{
			HTTPResponse returnV = new HTTPResponse();
			HttpResponseMessage response = new HttpResponseMessage();
			HttpClient client = new HttpClient();
			try
			{
				if (Method == "GET")
				{
					response = client.GetAsync(URL).Result;
				}
				if (Method == "POST")
				{
					response = client.PostAsync(URL, new StringContent(JSON)).Result;
				}
			}
			catch (WebException e)
			{
				returnV.Success = "SendError";
				returnV.Data = Convert.ToString(e);
				return returnV;
			}	
			if (response.StatusCode != HttpStatusCode.OK)
			{
				returnV.Success = "WebError";
				returnV.Data = $"Status code {(int)response.StatusCode} : {response.StatusCode.ToString()}";
				returnV.JSON = Newtonsoft.Json.Linq.JObject.Parse(response.Content.ReadAsStringAsync().Result);
				return returnV;
			}

			string data = response.Content.ReadAsStringAsync().Result;
			returnV.Success = "Data";
			returnV.Data = data;
			returnV.JSON = Newtonsoft.Json.Linq.JObject.Parse(data);
			return returnV;
		}
		
		static void Main(string[] args)
		{
			//Login as guest
			string access;
			try
			{
				access = File.ReadAllText($"{Directory.GetCurrentDirectory()}/guest.data");
				
			}
			catch
			{
				Console.WriteLine("Can't find login data, performing 1st time setup");
				HTTPResponse loginData = SendRequest("https://matrix.org/_matrix/client/r0/register?kind=guest", "POST", "{}");
				if (loginData.Success != "Data")
				{
					Console.WriteLine($"Error logging in: {loginData.Data}");
					goto Error;
				}
				access = loginData.JSON.Value<string>("access_token");
				
				File.WriteAllText($"{Directory.GetCurrentDirectory()}/guest.data", access);
			}


			//Get room
			string ID = "";
			while (ID == "")
			{
				Console.WriteLine("Please enter a room ID or alias (If no homeserver is given, matrix.org will be assumed)");
				ID = Console.ReadLine();
				if (ID.IndexOf(':') == -1) { ID = $"{ID}:matrix.org"; }
				try
				{
					if (ID.Substring(0, 1) == "#")
					{
						HTTPResponse nID = SendRequest($"https://matrix.org/_matrix/client/r0/directory/room/{Uri.EscapeDataString(ID)}", "GET", "");
						if (nID.Success != "Data")
						{
							Console.WriteLine($"Error converting Alias to ID: {nID.Data}");
							goto Error;
						}
						ID = nID.JSON.Value<string>("room_id");
					}
				}
				catch
				{
					ID = "";
				}
			}

			//Accept Privacy Policy / Join Server
			bool joined = false;
			while (!joined) {
				HTTPResponse Privacy = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{ID}/join?access_token={access}", "POST", "{}");
				if (Privacy.Success != "Data")
				{
					Console.WriteLine($"Since this is your first time using this guest account, please go accept the privacy policy by going to this link in your browser");
					Console.WriteLine(Privacy.JSON.Value<string>("consent_uri"));
					Console.WriteLine("Presss enter when you have done that");
					System.Diagnostics.Process.Start(Privacy.JSON.Value<string>("consent_uri"));
					Console.ReadLine();
				}
				else
				{
					joined = true;
				}
			}

			//Get messages
			HTTPResponse Messages = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{ID}/messages?dir=f&limit=100&access_token={access}", "GET", "");
			if (Messages.Success != "Data")
			{
				Console.WriteLine($"Error getting starting message: {Messages.Data}");
				goto Error;
			}
			string end = Messages.JSON.Value<string>("end");
			Console.WriteLine("Connected, Now reciving messages");
			bool keepon = true;
			while (keepon)
			{
				Messages = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{ID}/messages?from={end}&dir=f&limit=100&access_token={access}", "GET", "");
				if (Messages.Success != "Data")
				{
					Console.WriteLine($"Error getting messages: {Messages.Data}");
					goto Error;
				}
				Newtonsoft.Json.Linq.JArray array = (Newtonsoft.Json.Linq.JArray)Messages.JSON["chunk"];
				foreach (var item in array.Children())
				{
					try
					{
						Newtonsoft.Json.Linq.JObject itemJOb = item.ToObject<Newtonsoft.Json.Linq.JObject>();
						Console.WriteLine($"{itemJOb.Value<string>("user_id")}:");
						Console.WriteLine($"{itemJOb.Value<Newtonsoft.Json.Linq.JObject>("content").Value<String>("body")}");
						Console.WriteLine($"typeof {itemJOb.Value<Newtonsoft.Json.Linq.JObject>("content").Value<String>("msgtype")}");
						Console.WriteLine();
					}
					catch
					{
						Console.WriteLine($"Weird Message Found: ");
						Console.WriteLine(item);
						Console.WriteLine();
					}
				}
				end = Messages.JSON.Value<string>("end");
				Thread.Sleep(100);
			}

		//end
		Error:
			Console.WriteLine("Program finished! Press any key to exit");
			Console.ReadKey();
		}
	}
}
