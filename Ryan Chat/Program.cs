using System;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;

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

		static HTTPResponse SendRequest(string URL, string Method, string JSON, string Auth)
		{
			HTTPResponse returnV = new HTTPResponse();
			HttpResponseMessage response = new HttpResponseMessage();
			HttpClient client = new HttpClient();
			try
			{
				if (Method == "GET")
				{
					HttpRequestMessage request = new HttpRequestMessage()
					{
						RequestUri = new Uri(URL),
						Method = HttpMethod.Get
					};
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Auth);
					response = client.SendAsync(request).Result;
				}
				if (Method == "POST")
				{
					HttpRequestMessage request = new HttpRequestMessage()
					{
						RequestUri = new Uri(URL),
						Method = HttpMethod.Post,
						Content = new StringContent(JSON)
					};
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Auth);
					response = client.SendAsync(request).Result;
				}
				if (Method == "PUT")
				{
					HttpRequestMessage request = new HttpRequestMessage()
					{
						RequestUri = new Uri(URL),
						Method = HttpMethod.Put,
						Content = new StringContent(JSON)
					};
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Auth);
					response = client.SendAsync(request).Result;
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
			//Login
			int type = 0;
			while(type == 0) {
				Console.WriteLine("Do you wanna login as a \"Guest\" or a \"User\"? ");
				string output = Console.ReadLine();
				if (output == "Guest") { type = 1; }
				else if (output == "User") { type = 2; }
				else { Console.WriteLine("That is not an option! Please type the exact phrase \n"); }
			}
			if (type == 2) { Console.WriteLine("Sorry, but logging in as a user is not supported as of now, logging in as guest"); type = 1; }

			string access;
			if (type == 1) {
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
			}
			else
			{
				Console.WriteLine($"Error! Tried to login as user type {type}, exiting program");
				goto Error;
			}
			Console.WriteLine("Succesfully logged in! Now switching to main view");

			//Join Room Function
			string room = "";
			string lastMessage = "";
			bool GotoError = false;

			bool SwitchRooms(string roomID)
			{
				Console.Clear();
				Console.SetCursorPosition(0, 0);
				Console.Write("Switching Rooms...");
				HTTPResponse RoomsImIn = SendRequest($"https://matrix.org/_matrix/client/r0/joined_rooms?access_token={access}", "GET", "");
				Newtonsoft.Json.Linq.JArray array = (Newtonsoft.Json.Linq.JArray)RoomsImIn.JSON["joined_rooms"];
				bool UserInRoom = false;
				foreach (var item in array.Children())
				{
					if (roomID == item.ToString()) { UserInRoom = true; break; }
				}
				if (!UserInRoom)
				{
					int Ans = 0;
					while (Ans == 0)
					{
						Console.WriteLine("You are not in that room! would you like to join it ? (\"Yes\" or \"No\")");
						string output = Console.ReadLine();
						if (output == "Yes") { Ans = 2; }
						else if (output == "No") { Ans = 1; }
						else { Console.WriteLine("That is not an option! Please type the exact phrase \n"); }
					}
					if (Ans == 1) { return false; }
					TryToJoinRoom:;
					HTTPResponse Join = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{roomID}/join?access_token={access}", "POST", "{}");
					if (Join.Success != "Data")
					{
						Console.WriteLine("There Was an error joining room!");
						Console.WriteLine(Join.Data);
						Console.WriteLine(Join.JSON);
						Ans = 0;
						while (Ans == 0)
						{
							Console.WriteLine("Would you like to try again? (\"Yes\" or \"No\")");
							string output = Console.ReadLine();
							if (output == "Yes") { goto TryToJoinRoom; }
							else if (output == "No") { return false; }
							else { Console.WriteLine("That is not an option! Please type the exact phrase \n"); }
						}
					}
					else { room = roomID; }
				}
				else { room = roomID; }
				HTTPResponse Messages = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{roomID}/messages?dir=f&limit=100&access_token={access}", "GET", "");
				if (Messages.Success != "Data")
				{
					Console.WriteLine($"Error getting starting message: {Messages.Data}");
					GotoError = true;
				}
				lastMessage = Messages.JSON.Value<string>("end");
				return true;
			}


			//Main Loop
			Random rand = new Random();
			string command = "";
			string UserText = "";
			Stopwatch timer = new Stopwatch();
			timer.Start();
			ConsoleKeyInfo cki;
			List<String> Text = new List<String> {""};
			int Line = 0;
			int CursorPos = 0;
			bool Debug = false;
			bool Sound = false;
			bool MoveDown = true;
			SwitchRooms("!OGEhHVWSdvArJzumhm:matrix.org");
			if (GotoError) { goto Error; }
			if (room == "")
			{
				Console.WriteLine("Error! Can't join Matrix HQ room");
				goto Error;
			}
			Console.Clear();
			Text = new List<String> { $"Joined Room #matrix:matrix.org" };
			while (true) 
			{ 
				while(Console.KeyAvailable == false)
				{
					if(timer.ElapsedMilliseconds > 999)
					{
						timer.Restart();
						HTTPResponse Messages = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{room}/messages?from={lastMessage}&dir=f&limit=100&access_token={access}", "GET", "");
						//Text.Add(room);
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
								if (Debug)
								{
									Text.Add($"{itemJOb.Value<string>("user_id")}:");
									Text.Add($"{itemJOb.Value<Newtonsoft.Json.Linq.JObject>("content").Value<String>("body")}");
									Text.Add($"typeof {itemJOb.Value<Newtonsoft.Json.Linq.JObject>("content").Value<String>("msgtype")}");
									Text.Add("");
								}
								else
								{
									string Mess = $"{itemJOb.Value<string>("user_id")}: {itemJOb.Value<Newtonsoft.Json.Linq.JObject>("content").Value<String>("body")}";
									List<String> ThingsToAdd = new List<string> { };
									int StartAt = 0;
									for (int i = 0; i < Mess.Length; i++)
									{
										if (Mess[i] == '\n')
										{
											ThingsToAdd.Add(Mess.Substring(StartAt, i - StartAt));
											StartAt = i + 1;
										}
										if (i - StartAt == Console.WindowWidth - 1)
										{
											ThingsToAdd.Add(Mess.Substring(StartAt, i - StartAt));
											StartAt = i;
										}
									}
									ThingsToAdd.Add(Mess.Substring(StartAt));
									foreach(string s in ThingsToAdd)
									{
										Text.Add(s);
									}
								}
								if (Sound) { Console.Beep(); }
							}
							catch
							{
								Text.Add($"Weird Message Found: ");
								Text.Add(item.ToString());
								Text.Add("");
							}
						}
						lastMessage = Messages.JSON.Value<string>("end");
					}
					if (command != "")
					{
						Console.SetCursorPosition(0, Console.WindowHeight - 1);
						Console.Write(new string(' ', Console.WindowWidth - 1));

						if (command.Substring(0, 1) == "!")
						{
							String[] CommandArray = command.Split(' ');
							if (CommandArray[0] == "!room")
							{
								string ID = CommandArray[1];
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
								if (ID != "")
								{
									bool JoinedServer = SwitchRooms(ID);
									Console.Clear();
									if (JoinedServer) { Text = new List<String> { $"Joined room {CommandArray[1]}" }; }
									if (GotoError) { goto Error; }
								}
								else
								{
									Text.Add($"Couldn't join room {CommandArray[1]}");
								}
							}
							if (CommandArray[0] == "!exit")
							{
								Console.SetCursorPosition(0, Console.WindowHeight - 1);
								Console.Write("Bye! ");
								Thread.Sleep(500);
								goto Error;
							}
							if (CommandArray[0] == "!set")
							{
								if (CommandArray[1] == "!debug")
								{
									if (CommandArray[2] == "true" || CommandArray[2] == "yes" || CommandArray[2] == "on")
									{
										Debug = true;
										Console.SetCursorPosition(0, Console.WindowHeight - 1);
										Console.Write("Debug messages: ON");
									}
									if (CommandArray[2] == "false" || CommandArray[2] == "no" || CommandArray[2] == "off")
									{
										Debug = false;
										Console.SetCursorPosition(0, Console.WindowHeight - 1);
										Console.Write("Debug messages: OFF");
									}
								}
								if (CommandArray[1] == "sound")
								{
									if (CommandArray[2] == "true" || CommandArray[2] == "yes" || CommandArray[2] == "on")
									{
										Debug = true;
										Console.SetCursorPosition(0, Console.WindowHeight - 1);
										Console.Write("New message sounds: ON");
									}
									if (CommandArray[2] == "false" || CommandArray[2] == "no" || CommandArray[2] == "off")
									{
										Debug = false;
										Console.SetCursorPosition(0, Console.WindowHeight - 1);
										Console.Write("New message sounds: OFF");
									}
								}
							}
						}
						else
						{
							//Text.Add("Sending Message...");
							for(int i = 0; i < command.Length; i++)
							{
								if(i < command.Length - 1)
								{
									if(command.Substring(i, 2) == @"\\")
									{
										command = command.Substring(0, i) + @"\"+command.Substring(i + 2);
									}
									if (command.Substring(i, 2) == @"\n")
									{
										command = command.Substring(0, i) + "\n" + command.Substring(i + 2);
									}
									if (command.Substring(i, 2) == @"\!")
									{
										command = command.Substring(0, i) + @"!" + command.Substring(i + 2);
									}
								}
							}
							string Tnx = "";
							for (int i = 0; i < 10; i++)
							{
								char next = '0';
								int randnum = rand.Next(0, 61);
								if (randnum < 10) { next = (char)(randnum + 48); }
								else if(randnum < 36) { next = (char)(randnum + 55); }
								else { next = (char)(randnum + 61); }
								Tnx = Tnx + next;
							}
							//Text.Add(Tnx);
							string MessageJSON = "{" +
							"\"msgtype\": \"m.text\"," +
							$"\"body\": \"{command}\"" +
							"}";
							//Text.Add(MessageJSON);
							HTTPResponse Sent = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{room}/send/m.room.message/{Tnx}", "PUT", MessageJSON, access);
							//Text.Add(Sent.Data);
						}
						command = "";
					}
					if (MoveDown)
					{
						Line = Text.Count - Console.WindowHeight + 2;
					}
					if (Line > Text.Count - Console.WindowHeight + 2) { Line = Text.Count - Console.WindowHeight + 2; MoveDown = true; }
					if (Line < 0) { Line = 0; }
					Console.CursorVisible = false;
					for (int i = 0; i <  Math.Min(Text.Count, Console.WindowHeight - 2) ; i++)
					{
						Console.SetCursorPosition(0, i);
						Console.Write(Text[i + Line] + new string(' ', Math.Max(Console.WindowWidth - Text[i + Line].Length - 1, 0)));
					}
					Console.SetCursorPosition(Console.WindowWidth - 1, 0);
					if (MoveDown) { Console.Write("v"); } else { Console.Write("x"); }
					Console.SetCursorPosition(CursorPos, Console.WindowHeight - 1);
					Console.CursorVisible = true;
				}
				cki = Console.ReadKey(true);
				if(cki.Key == ConsoleKey.UpArrow) { Line--; MoveDown = false; }
				else if (cki.Key == ConsoleKey.DownArrow) { Line++; }
				else if (cki.Key == ConsoleKey.LeftArrow) { CursorPos--; if (CursorPos < 0) { CursorPos = 0; } }
				else if (cki.Key == ConsoleKey.RightArrow) { CursorPos++; if (CursorPos > Console.WindowWidth) { CursorPos = Console.WindowWidth; } if (CursorPos > UserText.Length) { CursorPos = UserText.Length; } }
				else if (cki.Key == ConsoleKey.PageUp) { Line -= Console.WindowHeight - 2; MoveDown = false; }
				else if (cki.Key == ConsoleKey.PageDown) { Line += Console.WindowHeight - 2; }
				else if (cki.Key == ConsoleKey.Enter) { command = UserText; UserText = ""; CursorPos = 0; }
				else if (cki.Key == ConsoleKey.Backspace && CursorPos != 0) { UserText = UserText.Substring(0, CursorPos - 1)+UserText.Substring(CursorPos); 
					if(UserText == "") { Console.SetCursorPosition(0, Console.WindowHeight - 1); Console.Write(" "); }
					CursorPos--;
				}
				else if (char.GetNumericValue(cki.KeyChar) == 7) { Console.Beep(); }
				else if (char.IsControl(cki.KeyChar)) { }
				else if (UserText.Length < Console.WindowWidth - 1){ UserText = UserText.Substring(0, CursorPos)+cki.KeyChar+UserText.Substring(CursorPos); CursorPos++; }
				else { Console.Beep(); }
				Console.SetCursorPosition(0, Console.WindowHeight - 1);
				if (UserText != "")
				{
					Console.Write(UserText + new string(' ', Console.WindowWidth - UserText.Length));
				}
				Console.SetCursorPosition(CursorPos, Console.WindowHeight - 1);
				Console.SetWindowPosition(0, 0);
			}




			//Get room
			//string ID = "";
			//while (ID == "")
			//{
			//	Console.WriteLine("Please enter a room ID or alias (If no homeserver is given, matrix.org will be assumed)");
			//	ID = Console.ReadLine();
			//	if (ID.IndexOf(':') == -1) { ID = $"{ID}:matrix.org"; }
			//	try
			//	{
			//		if (ID.Substring(0, 1) == "#")
			//		{
			//			HTTPResponse nID = SendRequest($"https://matrix.org/_matrix/client/r0/directory/room/{Uri.EscapeDataString(ID)}", "GET", "");
			//			if (nID.Success != "Data")
			//			{
			//				Console.WriteLine($"Error converting Alias to ID: {nID.Data}");
			//				goto Error;
			//			}
			//			ID = nID.JSON.Value<string>("room_id");
			//		}
			//	}
			//	catch
			//	{
			//		ID = "";
			//	}
			//}
			//
			////Accept Privacy Policy / Join Server
			//bool joined = false;
			//while (!joined) {
			//	HTTPResponse Privacy = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{ID}/join?access_token={access}", "POST", "{}");
			//	if (Privacy.Success != "Data")
			//	{
			//		Console.WriteLine($"Since this is your first time using this guest account, please go accept the privacy policy by going to this link in your browser");
			//		Console.WriteLine(Privacy.JSON.Value<string>("consent_uri"));
			//		Console.WriteLine("Presss enter when you have done that");
			//		System.Diagnostics.Process.Start(Privacy.JSON.Value<string>("consent_uri"));
			//		Console.ReadLine();
			//	}
			//	else
			//	{
			//		joined = true;
			//	}
			//}
			//
			////Get messages
			//HTTPResponse Messages = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{ID}/messages?dir=f&limit=100&access_token={access}", "GET", "");
			//if (Messages.Success != "Data")
			//{
			//	Console.WriteLine($"Error getting starting message: {Messages.Data}");
			//	goto Error;
			//}
			//string end = Messages.JSON.Value<string>("end");
			//Console.WriteLine("Connected, Now reciving messages");
			//bool keepon = true;
			//while (keepon)
			//{
			//	Messages = SendRequest($"https://matrix.org/_matrix/client/r0/rooms/{ID}/messages?from={end}&dir=f&limit=100&access_token={access}", "GET", "");
			//	if (Messages.Success != "Data")
			//	{
			//		Console.WriteLine($"Error getting messages: {Messages.Data}");
			//		goto Error;
			//	}
			//	Newtonsoft.Json.Linq.JArray array = (Newtonsoft.Json.Linq.JArray)Messages.JSON["chunk"];
			//	foreach (var item in array.Children())
			//	{
			//		try
			//		{
			//			Newtonsoft.Json.Linq.JObject itemJOb = item.ToObject<Newtonsoft.Json.Linq.JObject>();
			//			Console.WriteLine($"{itemJOb.Value<string>("user_id")}:");
			//			Console.WriteLine($"{itemJOb.Value<Newtonsoft.Json.Linq.JObject>("content").Value<String>("body")}");
			//			Console.WriteLine($"typeof {itemJOb.Value<Newtonsoft.Json.Linq.JObject>("content").Value<String>("msgtype")}");
			//			Console.WriteLine();
			//		}
			//		catch
			//		{
			//			Console.WriteLine($"Weird Message Found: ");
			//			Console.WriteLine(item);
			//			Console.WriteLine();
			//		}
			//	}
			//	end = Messages.JSON.Value<string>("end");
			//	Thread.Sleep(100);
			//}

		//end
		Error:
			Console.WriteLine("Program finished! Press any key to exit");
			Console.ReadKey();
		}
	}
}
