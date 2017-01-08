using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using RakudaSlack.RTM;
using System.Web;
using System.Runtime.Remoting.Messaging;

namespace RakudaSlack
{
    namespace RTM
    {
        static class Ext
        {
            public static string ToQString(this NameValueCollection that)
            {
                return string.Join("&", that.AllKeys.Select(x => x + "=" + HttpUtility.UrlEncode(that[x])));
            }
        }
        class Message
        {
            public string type { get; set; }
            public string channel { get; set; }
            public string user { get; set; }
            public string text { get; set; }
            public string ts { get; set; }
            public string team { get; set; }
        }
        class Reaction
        {
            public string type { get; set; }
            public string user { get; set; }
            public string reaction { get; set; }
            public string item_user { get; set; }
            public JObject item { get; set; }
            public string event_ts { get; set; }
        }
    }
    class PostedMessage
    {
        public bool ok { get; set; }
        public string ts { get; set; }
        public string channel { get; set; }
        public JObject message { get; set; }
    }
    class PostMessage
    {
        public event Action OnPosted;

        public string Channel { get; set; }
        public string Text { get; set; }
        public string Parse { get; set; } = "none";
        public string Username { get; set; } = null;
        public bool AsUser { get; set; } = !true;
        public string IconEmoji { get; set; } = null;
        public Slack Slack { get; set; }
        public PostedMessage PostedMessage { get; protected set; }

        public void Post()
        {
            if (PostedMessage == null)
            {
                var col = HttpUtility.ParseQueryString(string.Empty);
                col["token"] = Slack.Token;
                Construct(col);
                var wc = new WebClient();
                PostedMessage = JsonConvert.DeserializeObject<PostedMessage>(wc.DownloadString("https://slack.com/api/chat.postMessage?" + col.ToQString()));
                OnPosted?.Invoke();
            }
            else
            {
                var col = HttpUtility.ParseQueryString(string.Empty);
                col["token"] = Slack.Token;
                col["ts"] = PostedMessage.ts;
                Construct(col);
                var wc = new WebClient();
                JsonConvert.DeserializeObject<PostedMessage>(wc.DownloadString("https://slack.com/api/chat.update?" + col.ToQString()));
            }
        }
        public void Construct(NameValueCollection queryString)
        {

            queryString["channel"] = Channel;
            queryString["text"] = Text;
            queryString["parse"] = Parse;
            if (Username != null)
                queryString["username"] = Username;
            queryString["as_user"] = AsUser ? "true" : "false";
            if (IconEmoji != null)
                queryString["icon_emoji"] = IconEmoji;

        }

        public Reaction AddReaction(string name)
        {
            var react = new Reaction { Slack = Slack, Timestamp = PostedMessage.ts, Name = name, Channel = Channel };
            react.Add();
            return react;
        }
    }

    class Reaction
    {
        public Slack Slack;
        public string Name;
        public string File;
        public string FileComment;
        public string Channel;
        public string Timestamp;

        public void Add()
        {
            var col = System.Web.HttpUtility.ParseQueryString(string.Empty);
            col["token"] = Slack.Token;
            Construct(col);
            var wc = new WebClient();
            wc.DownloadString("https://slack.com/api/reactions.add?" + col.ToQString());
        }
        public void Construct(NameValueCollection queryString)
        {
            queryString["name"] = Name;
            if (File != null)
                queryString["file"] = File;
            if (FileComment != null)
                queryString["file_comment"] = FileComment;
            if (Channel != null)
                queryString["channel"] = Channel;
            if (Timestamp != null)
                queryString["timestamp"] = Timestamp;

        }
    }

    class Slack
    {
        public string Token { get; protected set; }
        public Slack(string token)
        {
            Token = token;
        }
        public void Connect()
        {
            var wc = new WebClient();
            var job = JObject.Parse(wc.DownloadString("https://slack.com/api/rtm.start?token=" + Uri.EscapeUriString(Token)));
            if (!job["ok"].Value<bool>())
            {
                Console.WriteLine("couldNt connect");
                return;
            }
            var wsurl = job["url"].Value<string>();
            var ws = new WebSocket(wsurl);
            string id = job["self"]["id"].Value<string>();
            User = id;

            ws.OnOpen += (sender, e) =>
            {
            };
            ws.OnMessage += (sender, e) =>
            {
                Console.WriteLine(e.Data);
                var json = JObject.Parse(e.Data);
                var a = json["type"].Value<string>();
                switch (a)
                {
                    case "message":
                        {
                            var text = JsonConvert.DeserializeObject<RTM.Message>(e.Data);
                            if (text.user == null || text.user == id)
                                break;
                            if (text.text.IndexOf(":") != -1)
                            {
                                Task.Run(() =>
                                {
                                    ProcessCommand(text)?.Post();
                                });
                            }
                        }
                        break;
                    case "reaction_removed":
                    case "reaction_added":
                        {
                            OnReaction?.Invoke(this, JsonConvert.DeserializeObject<RTM.Reaction>(e.Data));
                        }
                        break;
                }
            };

            ws.OnError += (sender, e) =>
            {
            };
            ws.OnClose += (sender, e) =>
            {
            };
            ws.Connect();
        }

        public static Slack Current
        {
            get { return CallContext.LogicalGetData("Slack") as Slack; }
            set { CallContext.LogicalSetData("Slack", value); }
        }

        public static PostMessage CurrentMessage
        {
            get { return CallContext.LogicalGetData("Slack.CurrentMessage") as PostMessage; }
            set { CallContext.LogicalSetData("Slack.CurrentMessage", value); }
        }

        public string User { get; private set; }

        private PostMessage ProcessCommand(RTM.Message x)
        {
            AliasCommand.RecCount = 0;
            CallContext.LogicalSetData("Slack", this);
            x.text = HttpUtility.HtmlDecode(x.text);
            var i = x.text.IndexOf(':');
            if (i == -1)
            {
                return null;
            }
            var p = CommandParser.ParseCommand(x.text, false);
            if (p.Command.Name == "alias")
            {
                p = CommandParser.ParseCommand(x.text, false, false);
                AliasDefine.AddAlias(p.Argument, p.Text);
                return null;
            }
            var bm = new PostMessage { Slack = this, Channel = x.channel};
            CurrentMessage = bm;
            bm.Text = p.Command.Process(p.Text, p.Argument);
            if (string.IsNullOrEmpty(bm.Text))
                return null;
            return (bm);
        }
        
        public event Action<Slack, RTM.Reaction> OnReaction;

        public string GetUserName(string user)
        {
            var wc = new WebClient();
            var j = JObject.Parse(wc.DownloadString($"https://slack.com/api/users.info?token={HttpUtility.UrlEncode(Token)}&user={HttpUtility.UrlEncode(user)}"));
            return j["user"]["name"].Value<string>();
        }
    }
}
