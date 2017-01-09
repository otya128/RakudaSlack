using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace RakudaSlack
{
    public class File
    {
        public string id { get; set; }
        public int created { get; set; }
        public int timestamp { get; set; }
        public string name { get; set; }
        public string title { get; set; }
        public string mimetype { get; set; }
        public string filetype { get; set; }
        public string pretty_type { get; set; }
        public string user { get; set; }
        public bool editable { get; set; }
        public int size { get; set; }
        public string mode { get; set; }
        public bool is_external { get; set; }
        public string external_type { get; set; }
        public bool is_public { get; set; }
        public bool public_url_shared { get; set; }
        public bool display_as_bot { get; set; }
        public string username { get; set; }
        public string url_private { get; set; }
        public string url_private_download { get; set; }
        public string permalink { get; set; }
        public string permalink_public { get; set; }
        public string edit_link { get; set; }
        public string preview { get; set; }
        public string preview_highlight { get; set; }
        public int lines { get; set; }
        public int lines_more { get; set; }
        public List<object> channels { get; set; }
        public List<object> groups { get; set; }
        public List<object> ims { get; set; }
        public int comments_count { get; set; }
    }

    public class Message
    {
        public string type { get; set; }
        public string subtype { get; set; }
        public string text { get; set; }
        public File file { get; set; }
        public string user { get; set; }
        public bool upload { get; set; }
        public bool display_as_bot { get; set; }
        public string username { get; set; }
        public object bot_id { get; set; }
        public string channel { get; set; }
        public string ts { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            CommandRegister.Regist(new EchoCommand2());
            CommandRegister.Regist(new Echo());
            CommandRegister.Regist(new Filter("おちゃめ", ochame));
            CommandRegister.Regist(new Filter("んちゃめ", nchame));
            CommandRegister.Regist(new Filter("んもうだめ", nmoudame));
#if RAKUDALANG
            CommandRegister.Regist(new Rakuda());
#endif
            CommandRegister.Regist(new Pipe());
            CommandRegister.Regist(new Repeat());
            CommandRegister.Regist(new Plus());
            CommandRegister.Regist(new Minus());
            CommandRegister.Regist(new Mul());
            CommandRegister.Regist(new Div());
            CommandRegister.Regist(new Mod());
            CommandRegister.Regist(new otyaCommand());
            CommandRegister.Regist(new Gobi());
            CommandRegister.Regist(new AliasDefine());
            CommandRegister.Regist(new Replace());
            CommandRegister.Regist(new Append());
#if GOOGLETRANSLATE
            CommandRegister.Regist(new GoogleTranslate());
#endif
            CommandRegister.Regist(new EqualCommand2());
            CommandRegister.Regist(new Equal());
            CommandRegister.Regist(new Kao());
            CommandRegister.Regist(new CommandRandom());
            CommandRegister.Regist(new Array());
            CommandRegister.Regist(new Array2());
            CommandRegister.Regist(new ArrayAccess());
            CommandRegister.Regist(new CommandSubstr());
            CommandRegister.Regist(new CommandChr());
            CommandRegister.Regist(new CommandLen());
            CommandRegister.Regist(new Json());
#if MECAB
            CommandRegister.Regist(new SuperMecab());
#endif
            CommandRegister.Regist(new ArrayItem());
            CommandRegister.Regist(new StrLen());
            CommandRegister.Regist(new ArrayLen());
            CommandRegister.Regist(new If());
            CommandRegister.Regist(new GetLocal());
            CommandRegister.Regist(new SetLocal());
            CommandRegister.Regist(new HasLocal());
            CommandRegister.Regist(new RegexReplace());
            CommandRegister.Regist(new WGet());
            CommandRegister.Regist(new RegexIsMatch());
            CommandRegister.Regist(new BASIC.BASIC());
            //CommandRegister.Regist(new Browser());
            CommandRegister.Regist(new OnReaction());
            CommandRegister.Regist(new OnReactionRemoved());
            CommandRegister.Regist(new OnPosted());
            CommandRegister.Regist(new AddReaction());
            CommandRegister.Regist(new SetIconEmoji());
            CommandRegister.Regist(new Post());
            CommandRegister.Regist(new SetPost());
            CommandRegister.Regist(new GetPost());
            CommandRegister.Regist(new Edit());
            CommandRegister.Regist(new SetTimeout());
            CommandRegister.Regist(new GetReactionCount());

            AliasDefine.LoadAlias();
            if (args.Length == 0)
            {
                Console.WriteLine("token");
                return;
            }
            fuga(args[0]);
            //hoge(args[0]);
            Thread.Sleep(-1);
        }
        static void fuga(string token)
        {
            var slack = new Slack(token);
            slack.Connect();
        }
        static string nmoudame(string source)
        {
            return System.Web.HttpUtility.HtmlDecode(new WebClient().DownloadString("http://tekito.kanichat.com/nmoudame/response.php?length=6&normal=1&small=1&dots=1&ltu=1&ltu_prob=30&ex=1&ex_prob=30&c=0&str=" + source).Replace("<?xml version=\"1.0\"?>", "").Replace("<string>", "").Replace("</string>", "").Replace("<result>", "").Replace("</result>", "").Replace("\r", "").Replace("\n", ""));
        }
        static string nchame(string source)
        {
            return ochame(nmoudame(source));
        }
        static string ochame(string source)
        {
            return ochameex(ochamegobi(source)).Replace("。", "にょ。");//.Replace("?", "にょ？").Replace("？", "にょ？");
        }
        static string ochameex(string source)
        {
            var regex = new Regex("([!！?？]+)");
            source = source.Replace("り、", "った。");
            source = source.Replace("て、", "た。");
            //source = source.Replace("かも", "かも。");
            source = regex.Replace(source, "にょ$1");
            return source;
        }
        static string ochamegobi(string source)
        {
            source = source.Replace("よ。", "。").Replace("いません。", "ない。").Replace("下さい。", "。").Replace("ありません。", "ない。").Replace("ください。", "。").Replace("いたします。", "する。").Replace("します。", "する。").Replace("でしょう。", "だ。").Replace("ね。", "。").Replace("やめましょう。", "やめる。").Replace("ましょう。", "する。").Replace("しょう。", "る。").Replace("ですよ。", "。").Replace("です。", "。").Replace("ります。", "る。").Replace("ます。", "る。");
            return source;
        }
        static string gobi(string source, string gobi)
        {
            return ochameex(ochamegobi(source), gobi).Replace("。", gobi + "。");//.Replace("?", "にょ？").Replace("？", "にょ？");
        }
        static string ochameex(string source, string gobi)
        {
            var regex = new Regex("([!！?？]+)");
            source = source.Replace("り、", "った。");
            source = source.Replace("て、", "た。");
            //source = source.Replace("かも", "かも。");
            source = regex.Replace(source, gobi + "$1");
            return source;
        }
    }
}
