using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace RakudaSlack
{
    abstract class CommandExpr
    {

    }
    class CommandSet : CommandExpr
    {
        public CommandExpr Expr1;
        public CommandExpr Expr2;
        public CommandSet(CommandExpr expr1, CommandExpr expr)
        {
            Expr1 = expr1;
            Expr2 = expr;
        }
        public override string ToString()
        {
            return "(set " + Expr1.ToString() + " " + Expr2.ToString() + ")";
        }
    }
    class CommandVoid : CommandExpr
    {
        public CommandExpr Expr;
        public CommandVoid(CommandExpr expr)
        {
            Expr = expr;
        }
        public override string ToString()
        {
            return "(void " + Expr.ToString() + ")";
        }
    }
    class CommandThis : CommandExpr
    {
        public override string ToString()
        {
            return "(this)";
        }
    }
    class CommandBinOp : CommandExpr
    {
        public CommandExpr Left;
        public CommandExpr Right;
        public CommandBinOp(CommandExpr l, CommandExpr r)
        {
            Left = l;
            Right = r;
        }
        public override string ToString()
        {
            return "(binop " + Left.ToString() + " " + Right.ToString() + ")";
        }
    }
    class CommandPipe : CommandBinOp
    {
        public CommandPipe(CommandExpr l, CommandExpr r) : base(l, r)
        {
        }
        public override string ToString()
        {
            return "(" + Left.ToString() + "|" + Right.ToString() + ")";
        }
    }
    class CommandIf : CommandExpr
    {
        public CommandExpr Condition;
        public CommandExpr Then;
        public CommandExpr Else;
        public CommandIf(CommandExpr c, CommandExpr t, CommandExpr e)
        {
            Condition = c;
            Then = t;
            Else = e;
        }
        //if eq(1,1) then 1 else 3
        public override string ToString()
        {
            return "(if " + Condition.ToString() + " then " + Then.ToString() + " else " + Else.ToString() + ")";
        }
    }
    class CommandDot : CommandBinOp
    {
        public CommandDot(CommandExpr l, CommandExpr r)
            : base(l, r)
        {
        }
        public override string ToString()
        {
            return "(" + Left.ToString() + "." + Right.ToString() + ")";
        }
    }
    class CommandCall : CommandExpr
    {
        public CommandExpr Command;
        public List<CommandExpr> Argument;
        public bool IsTail { get; set; }
        public CommandCall(CommandExpr c, List<CommandExpr> arg)
        {
            Command = c;
            Argument = arg;
        }
        void AddArgument(CommandExpr expr)
        {
            Argument.Add(expr);
        }
        public override string ToString()
        {
            if (Argument.Count > 0)
                return "" + Command.ToString() + " (" + Argument.Select(x => x.ToString()).Aggregate((x, y) => y + "," + x) + ")";
            else
            {
                return "" + Command.ToString() + " ()";
            }
        }
    }
    class CommandValue : CommandExpr
    {
        public string Value;
        public CommandValue(string value)
        {
            Value = value;
        }
        public override string ToString()
        {
            return "" + Value.ToString() + "";
        }
    }
    class CommandString : CommandExpr
    {
        public string Value;
        public CommandString(string value)
        {
            Value = value;
        }
        public override string ToString()
        {
            return "\"" + Value.ToString() + "\"";
        }
    }
    class CommandLambda : CommandExpr
    {
        public CommandExpr Expr;
        public List<string> ArgumentName;
        public CommandLambda(List<string> a, CommandExpr e)
        {
            ArgumentName = a;
            Expr = e;
            OptTailCall(e);
        }
        void OptTailCall(CommandExpr e)
        {
            if (e is CommandVoid)
            {
                var v = e as CommandVoid;
                OptTailCall(v.Expr);
            }
            if (e is CommandPipe)
            {
                var p = e as CommandPipe;
                OptTailCall(p.Right);
            }
            if (e is CommandIf)
            {
                var i = e as CommandIf;
                OptTailCall(i.Then);
                OptTailCall(i.Else);
            }
            if (e is CommandCall)
            {
                var c = e as CommandCall;
                c.IsTail = true;
            }
        }
        public override string ToString()
        {
            if (ArgumentName.Count > 0)
                return "(command (" + ArgumentName.Aggregate((x, y) => y + "," + x) + ") " + Expr.ToString() + ")";
            else
            {
                return "(command () " + Expr.ToString() + ")";
            }
        }
    }
    //let a expr|
    /*
     * name ::= (Char.IsLetter)*
     * argument-list ::= ( expr ("," expr)* | 3)
     * term ::= name "(" argument-list ")" | "(" expr ")"
     * expr ::= term ( "|" term )*
     * */
    class CommandParserEx
    {
        string Input;
        IEnumerator<Token> Lex;
        public CommandParserEx(string input)
        {
            Input = input;
            Lex = NextToken(Input);//.GetEnumerator();
        }

        Exception Error()
        {
            return new Exception();
        }
        List<CommandExpr> ArgumentList()
        {
            List<CommandExpr> l = new List<CommandExpr>();
            Lex.MoveNext();
            while (true)
            {
                var token = Lex.Current;
                if (token.Type == TokenType.RParen)
                {
                    Lex.MoveNext();
                    return l;
                }
                l.Add(Expr());
                token = Lex.Current;
                if (token.Type == TokenType.Comma)
                {
                    Lex.MoveNext();
                }
            }
        }
        List<string> FuncArgumentList()
        {
            List<string> l = new List<string>();
            //Lex.MoveNext();
            var token = Lex.Current;
            if (token.Type != TokenType.LParen)
            {
                throw Error();
            }
            Lex.MoveNext();
            token = Lex.Current;
            while (true)
            {
                token = Lex.Current;
                if (token.Type == TokenType.RParen)
                {
                    Lex.MoveNext();
                    return l;
                }
                if (TokenType.Iden != token.Type)
                    throw Error();
                l.Add(token.Value);
                Lex.MoveNext();
                token = Lex.Current;
                if (token.Type == TokenType.Comma)
                {
                    Lex.MoveNext();
                }
            }
        }
        CommandExpr Term()
        {
            var token = Lex.Current;
            switch (token.Type)
            {
                case TokenType.LParen:
                    {
                        Lex.MoveNext();
                        var expr = Expr();
                        token = Lex.Current;
                        if (token.Type != TokenType.RParen)
                            throw Error();
                        Lex.MoveNext();
                        return expr;
                    }
                case TokenType.Iden:
                    {
                        Lex.MoveNext();
                        if (token.Value == "command")
                        {
                            var arg = FuncArgumentList();
                            var expr = Expr();
                            return new CommandLambda(arg, expr);
                        }
                        if (token.Value == "void")
                        {
                            token = Lex.Current;
                            if (token.Type == TokenType.RParen)
                                return new CommandVoid(null);
                            var expr = Expr();
                            return new CommandVoid(expr);
                        }
                        if (token.Value == "set")
                        {
                            //set iden expr
                            //set expr.iden expr     -> doukai     suru  ?
                            //set expr.expr.iden expr-> d     shaku    ka
                            var expr1 = Expr();
                            if (Lex.Current.Type == TokenType.Comma)
                                Lex.MoveNext();
                            var expr2 = Expr();
                            return new CommandSet(expr1, expr2);
                        }
                        if (token.Value == "if")
                        {
                            var cond = Expr();
                            token = Lex.Current;
                            if (token.Value == "then")
                            {
                                Lex.MoveNext();
                            }
                            var then = Expr();
                            token = Lex.Current;
                            if (token.Value == "else")
                            {
                                Lex.MoveNext();
                            }
                            var @else = Expr();
                            return new CommandIf(cond, then, @else);
                        }
                        /*var token2 = Lex.Current;
                        if (token2.Type == TokenType.LParen)
                        {
                            return new CommandCall(ArgumentList());
                        }*/
                        return new CommandValue(token.Value);
                    }
                case TokenType.String:
                    Lex.MoveNext();
                    return new CommandString(token.Value);
                case TokenType.Dot:
                    Lex.MoveNext();
                    return new CommandThis();
                default:
                    break;
            }
            throw Error();
        }
        public CommandExpr DotExpr()
        {
            var token = Lex.Current;
            if (token == null)
            {
                Lex.MoveNext();
                token = Lex.Current;
            }
            var term = Term();
            CommandBinOp op = null;
            while (true)
            {
                token = Lex.Current;
                if (token.Type != TokenType.Dot)
                {
                    if (op == null)
                    {
                        return term;
                    }
                    return op;
                }
                Lex.MoveNext();
                var term2 = Term();
                if (op == null)
                {
                    op = new CommandDot(term, term2);
                }
                else
                {
                    op = new CommandDot(op, term2);
                }
            }
            throw Error();
        }
        public CommandExpr CallExpr()
        {
            //Lex.MoveNext();
            var token = Lex.Current;
            if (token == null)
            {
                Lex.MoveNext();
                token = Lex.Current;
            }
            var term = DotExpr();
            CommandCall expr = null;
            while (true)
            {
                token = Lex.Current;
                if (token.Type != TokenType.LParen)
                {
                    if (expr == null)
                    {
                        //Lex.MoveNext();
                        return term;
                    }
                    return expr;
                }
                //  Lex.MoveNext();
                var term2 = ArgumentList();
                if (expr == null)
                {
                    expr = new CommandCall(term, term2);
                }
                else
                {
                    expr = new CommandCall(expr, term2);
                }
            }
            throw Error();
        }
        public CommandExpr Expr()
        {
            //Lex.MoveNext();
            var token = Lex.Current;
            if (token == null)
            {
                Lex.MoveNext();
                token = Lex.Current;
            }
            var term = CallExpr();
            CommandPipe pipe = null;
            while (true)
            {
                token = Lex.Current;/*
                if (token.Type == TokenType.LParen)
                {
                    var t2 = new CommandCall(pipe == null ? term : pipe, ArgumentList());
                    if (pipe == null)
                    {
                        term = t2;//pipe = new CommandPipe(term, t2);
                    }
                    else
                    {
                        pipe = new CommandPipe(pipe, t2);
                    }
                    continue;
                }*/
                if (token.Type != TokenType.Pipe)
                {
                    if (pipe == null)
                    {
                        //Lex.MoveNext();
                        return term;
                    }
                    return pipe;
                }
                Lex.MoveNext();
                var term2 = CallExpr();
                if (pipe == null)
                {
                    pipe = new CommandPipe(term, term2);
                }
                else
                {
                    pipe = new CommandPipe(pipe, term2);
                }
            }
            throw Error();
        }
        public int index = 0;
        IEnumerator<Token> NextToken(string input)
        {
            for (; index < input.Length; index++)
            {
                char p = input[index];
                if (Char.IsWhiteSpace(p)) continue;
                if (Char.IsLetter(p) || Char.IsDigit(p) || p == '_')
                {
                    int jndex = index + 1;
                    for (; jndex < input.Length; jndex++)
                    {
                        p = input[jndex];
                        if (!(Char.IsLetter(p) || Char.IsDigit(p) || p == '_'))
                        {
                            break;
                        }
                    }
                    yield return new Token(TokenType.Iden, input.Substring(index, jndex - index));
                    index = jndex - 1;
                    continue;
                }
                switch (p)
                {
                    case '"':
                        int jndex = index + 1;
                        bool escape = false;
                        StringBuilder b = new StringBuilder();
                        for (; jndex < input.Length; jndex++)
                        {
                            p = input[jndex];
                            if (escape)
                            {
                                escape = false;
                                switch (p)
                                {
                                    case 't':
                                        p = '\t';
                                        break;
                                    case 'r':
                                        p = '\r';
                                        break;
                                    case 'n':
                                        p = '\n';
                                        break;
                                    case '"':
                                    default:
                                        break;
                                }
                                b.Append(p);
                                continue;
                            }
                            if (p == '\\')
                            {
                                escape = true;
                                continue;
                            }
                            if (p == '"')
                            {
                                yield return new Token(TokenType.String, b.ToString());//input.Substring(index + 1, jndex - index - 1));
                                break;
                            }
                            b.Append(p);
                        }
                        index = jndex;
                        continue;
                    case '(':
                        yield return new Token(TokenType.LParen);
                        continue;
                    case ')':
                        yield return new Token(TokenType.RParen);
                        continue;
                    case '|':
                        yield return new Token(TokenType.Pipe);
                        continue;
                    case ',':
                        yield return new Token(TokenType.Comma);
                        continue;
                    case ':':
                        yield return new Token(TokenType.End);
                        continue;
                    case '.':
                        yield return new Token(TokenType.Dot);
                        continue;
                    default:
                        throw new ArgumentException();
                }
            }
            yield return new Token(TokenType.End);
        }
    }
    [StructLayout(LayoutKind.Explicit)]
    struct CommandValueType
    {
        [FieldOffset(0)]
        public double Number;
    }
    public enum CommandObjectType
    {
        Void,
        Object,
        String,
        Number,
        Lambda,
        Array,
        TailSpecial,
        TailSpecialVoid,
    }
    class VarMap
    {
        public CommandObject This;
        public VarMap(VarMap parent)
        {
            Parent = parent;
            Map = new Dictionary<string, CommandObject>();
        }
        public VarMap Parent;
        public Dictionary<string, CommandObject> Map;
        public CommandObject Find(string name)
        {
            VarMap t = this;
            while (t != null)
            {
                if (t.Map.ContainsKey(name))
                {
                    return t.Map[name];
                }
                t = t.Parent;
            }
            return new CommandObject(name);
        }

        bool Set2(string name, CommandObject e)
        {
            if (Map.ContainsKey(name))
            {
                Map[name] = e;
                return true;
            }
            if (Parent != null)
            {
                return Parent.Set2(name, e);
            }
            return false;
        }
        public void Set(string name, CommandObject e)
        {
            if (!Set2(name, e))
            {
                Map.Add(name, e);
            }
        }
        public void Merge(VarMap from)
        {
            foreach (var i in from.Map)
            {
                if (Map.ContainsKey(i.Key))
                {
                    Map[i.Key] = i.Value;
                }
                else
                {
                    Map.Add(i.Key, i.Value);
                }
            }
        }
    }
    class CommandLambdaObject
    {
        public VarMap Map;
        public CommandExpr Expr;
        public List<string> Argument;
        CommandRunner CommandRunner;
        public CommandLambdaObject(List<string> arg, VarMap m, CommandExpr c, CommandRunner a)
        {
            Argument = arg;
            Map = m;
            Expr = c;
            CommandRunner = a;
        }
        LambdaWrapperCommand WrapperCommand;
        public override string ToString()
        {
            if (WrapperCommand == null)
            {
                WrapperCommand = new LambdaWrapperCommand(CommandRunner, Map, new CommandObject(CommandObjectType.Lambda, this));
                CommandRegister.Regist(WrapperCommand);
            }

            return WrapperCommand.Name;
        }
    }
    public struct CommandObject
    {
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            return obj is CommandObject ? (CommandObject)obj == this : false;
        }
        public static bool operator !=(CommandObject c1, CommandObject c2)
        {
            return !(c1 == c2);
        }
        public static bool operator ==(CommandObject c1, CommandObject c2)
        {
            return c1.Type == c2.Type && (c1.Object == null ? c1.Object == c2.Object : c1.Object.Equals(c2.Object));
        }
        public CommandObjectType Type;
        public object Object;
        public string String
        {
            get
            {
                return (string)Object;
            }
            set
            {
                Object = value;
            }
        }
        public bool Bool
        {
            get
            {
                return String == "true" ? true : false;
            }
        }
        public CommandObject[] Array
        {
            get
            {
                return (CommandObject[])Object;
            }
            set
            {
                Object = value;
            }
        }
        internal CommandLambdaObject Lambda
        {
            get
            {
                return (CommandLambdaObject)Object;
            }
            set
            {
                Object = value;
            }
        }
        public CommandObject(CommandObjectType type)
        {
            Type = type;
            Object = null;
        }
        public CommandObject(CommandObjectType type, object obj)
        {
            Type = type;
            Object = obj;
        }
        public CommandObject(string obj)
            : this(CommandObjectType.String, obj)
        {
        }
        public CommandObject(CommandObject[] obj)
            : this(CommandObjectType.Array, obj)
        {
        }
        public override string ToString()
        {
            switch (Type)
            {
                case CommandObjectType.Object:
                    return Object.ToString();
                case CommandObjectType.String:
                    return String;
                case CommandObjectType.Number:
                    break;
                case CommandObjectType.Lambda:
                    return Lambda.ToString();
                default:
                    break;
            }
            return "";
        }
    }
    public interface ICommand2
    {
        string Name { get; }
        CommandObject Process(CommandObject[] arg);
    }
    class LambdaWrapperCommand : ICommand
    {
        public LambdaWrapperCommand(CommandRunner a, VarMap b, CommandObject c)
        {
            CommandRunner = a;
            Command = c;
            Scope = b;
            LambdaName = "func" + string.Join("", Guid.NewGuid().ToByteArray().Select(x => x.ToString("X02")));
        }
        public string Name
        {
            get
            {
                return LambdaName;
            }
        }
        CommandRunner CommandRunner;
        CommandObject Command;
        VarMap Scope;
        string LambdaName;

        public string Process(string msg, string arg)
        {
            var args = arg.Split(',');
            CommandObject[] co = new CommandObject[1 + (arg.Length != 0 ? args.Length : 0)];
            int i2 = 0;
            co[i2] = new CommandObject(msg);
            if (arg.Length != 0)
                foreach (var i in args)
                {
                    i2++;
                    co[i2] = new CommandObject(args[i2 - 1]);
                }
            return CommandRunner.CallCommand(Command, co, Scope).ToString();
        }
    }
    class FuncWrapperCommand : ICommand
    {
        private Func<string, string, string> func;
        public FuncWrapperCommand(Func<string, string, string> func)
        {
            this.func = func;
            LambdaName = "func" + string.Join("", Guid.NewGuid().ToByteArray().Select(x => x.ToString("X02")));
        }
        public string Name
        {
            get
            {
                return LambdaName;
            }
        }
        string LambdaName;

        public string Process(string msg, string arg)
        {
            return func(msg, arg);
        }
    }
    //void(let(a,lambda((x),lambda((y),plus(x,y))))|void(a(2)(3))
    class CommandRunner
    {
        internal CommandObject CallCommand(CommandObject cmd, CommandObject[] args, VarMap map)
        {
            if (cmd.Type == CommandObjectType.String)
            {
                if (cmd.String == "wrap")
                {
                    return new CommandObject(cmd.ToString());
                }
                if (cmd.String == "require")
                {
                    foreach (var x in args)
                    {
                        var runner = new CommandRunner();
                        var w = x.ToString();
                        if (x.Type == CommandObjectType.String)
                        {
                            w = CallCommand(x, new CommandObject[0], map).ToString();
                        }
                        var ret = runner.Run(w, "");
                        map.Merge(runner.Map);
                        return ret;
                    }
                    return new CommandObject(CommandObjectType.Void, null);
                }
                if (cmd.String == "eval")
                {
                    return args.Select(x =>
                        {
                            var runner = new CommandRunner();
                            var ret = runner.Run(x.ToString(), "");
                            map.Merge(runner.Map);
                            return ret;
                        }).Last();
                }
                if (!CommandRegister.CommandList2.ContainsKey(cmd.String))
                {
                    cmd = map.Find(cmd.String);
                    if (cmd.Type != CommandObjectType.String)
                    {
                        return CallCommand(cmd, args, map);
                    }
                    Debug.WriteLine("NotFound:{0}", cmd.String);
                }
                return CommandRegister.CommandList2[cmd.String].Process(args);
            }
            else if (cmd.Type == CommandObjectType.Lambda)
            {
                var l = cmd.Lambda;
                var j = 0;
                var m = new VarMap(l.Map);
                foreach (var i in args)
                {
                    if (m.Map.ContainsKey(l.Argument[j]))
                        m.Map[l.Argument[j]] = i;
                    else
                        m.Map.Add(l.Argument[j], i);
                    j++;
                }
                m.This = new CommandObject(CommandObjectType.Lambda, new CommandLambdaObject(l.Argument, m, l.Expr, this));
                var ret = Eval(l.Expr, m);
                return ret;
            }
            else
            {
                throw new Exception();
            }
        }
        bool HasDefaultArg;
        bool IsFirstExpr;
        CommandObject DefaultArg;
        CommandObject Eval(CommandExpr expr1, VarMap map)
        {
            if (!(expr1 is CommandCall))
            {
                IsFirstExpr = false;
            }
            if (expr1 is CommandIf)
            {
                var expr = (CommandIf)expr1;
                var cond = Eval(expr.Condition, map);
                if (cond.Bool)
                {
                    return Eval(expr.Then, map);
                }
                else
                {
                    return Eval(expr.Else, map);
                }
                return new CommandObject(CommandObjectType.Void);
            }
            if (expr1 is CommandVoid)
            {
                var expr = (CommandVoid)expr1;
                if (expr.Expr == null)
                {
                    return new CommandObject(CommandObjectType.Void);
                }
                var ret = Eval(expr.Expr, map);
                if (ret.Type == CommandObjectType.TailSpecialVoid || ret.Type == CommandObjectType.TailSpecial)
                {
                    ret.Type = CommandObjectType.TailSpecialVoid;
                    return ret;
                }
                return new CommandObject(CommandObjectType.Void);
            }
            if (expr1 is CommandSet)
            {
                var expr = (CommandSet)expr1;
                var var = expr.Expr1;
                var vmap = map;
                string name = "";
                if (var is CommandDot)
                {
                    var dot = (CommandDot)var;
                    var left = Eval(dot.Left, map);
                    var right = Eval(dot.Right, map);
                    vmap = left.Lambda.Map;
                    name = right.String;
                }
                else if (var is CommandValue)
                {
                    name = ((CommandValue)var).Value;
                }
                else
                {
                    name = Eval(var, map).String;
                }
                var e = Eval(expr.Expr2, map);
                vmap.Set(name, e);
                return e;
            }
            if (expr1 is CommandLambda)
            {
                var expr = (CommandLambda)expr1;
                var lambdamap = map;//new VarMap(map);
                var lo = new CommandLambdaObject(expr.ArgumentName, lambdamap, expr.Expr, this);
                var obj = new CommandObject(CommandObjectType.Lambda, lo);
                //lo.Map.This = obj;
                return obj;
            }
            if (expr1 is CommandValue)
            {
                var expr = (CommandValue)expr1;
                if (CommandRegister.CommandList2.ContainsKey(expr.Value))
                {
                    return new CommandObject(CommandObjectType.String, expr.Value);
                }
                return (map.Find(expr.Value));
            }
            if (expr1 is CommandString)
            {
                var expr = (CommandString)expr1;
                return new CommandObject(CommandObjectType.String, expr.Value);
            }
            if (expr1 is CommandCall)
            {
                var expr = (CommandCall)expr1;
                var func = Eval(expr.Command, map);
                var arg = expr.Argument.Select(x => Eval(x, map));
                if (HasDefaultArg && IsFirstExpr)
                {
                    HasDefaultArg = false;
                    arg = arg.Concat(new[] { DefaultArg });
                }
                if (expr.IsTail)
                {
                    return new CommandObject(CommandObjectType.TailSpecial, Tuple.Create(func, arg.ToArray(), map));
                }
                var ret = CallCommand(func, arg.ToArray(), map);
                var type = ret.Type;
                while (ret.Type == CommandObjectType.TailSpecial || ret.Type == CommandObjectType.TailSpecialVoid)
                {
                    var a = ret.Object as Tuple<CommandObject, CommandObject[], VarMap>;
                    ret = CallCommand(a.Item1, a.Item2, a.Item3);
                }
                if (type == CommandObjectType.TailSpecialVoid)
                {
                    ret = new CommandObject(CommandObjectType.Void);
                }
                return ret;
            }
            if (expr1 is CommandThis)
            {
                var expr = (CommandThis)expr1;
                return map.This;
            }
            if (expr1 is CommandDot)
            {
                var expr = (CommandDot)expr1;
                var left = Eval(expr.Left, map);
                var right = Eval(expr.Right, map);

                return left.Lambda.Map.Find(right.String);
            }
            if (expr1 is CommandPipe)
            {
                var expr = (CommandPipe)expr1;
                var left = EvalArg(expr.Left, map);
                var cmd = left.Item1;
                var arg = left.Item2;
                if (HasDefaultArg)
                {
                    HasDefaultArg = false;
                    arg = arg.Concat(new[] { DefaultArg });//CallCommand(left, new[] { DefaultArg });
                }
                var leftret = left.Item3 ? CallCommand(cmd, arg.ToArray(), map) : cmd;
                if (leftret.Type == CommandObjectType.TailSpecial || leftret.Type == CommandObjectType.TailSpecialVoid)
                {
                    var type = leftret.Type;
                    var a = leftret.Object as Tuple<CommandObject, CommandObject[], VarMap>;
                    leftret = CallCommand(a.Item1, a.Item2, a.Item3);
                    if (type == CommandObjectType.TailSpecialVoid)
                    {
                        leftret = new CommandObject(CommandObjectType.Void);
                    }
                }
                //ex:A | B :A->
                //B(A("A"))
                var right = EvalArg(expr.Right, map);
                var cmdr = right.Item1;
                if (cmdr.Type == CommandObjectType.TailSpecial/* || cmdr.Type == CommandObjectType.TailSpecialVoid*/)
                {
                    var type = cmdr.Type;
                    var a = cmdr.Object as Tuple<CommandObject, CommandObject[], VarMap>;
                    cmdr = CallCommand(a.Item1, a.Item2, a.Item3);
                    if (type == CommandObjectType.TailSpecialVoid)
                    {
                        cmdr = new CommandObject(CommandObjectType.Void);
                    }
                }
                if (cmdr.Type == CommandObjectType.TailSpecialVoid)
                    return cmdr;
                var argr = right.Item2;
                if (leftret.Type != CommandObjectType.Void)
                    argr = argr.Concat(new[] { leftret });
                if (leftret.Type == CommandObjectType.Void && !right.Item3)
                {
                    return cmdr;
                }
                return CallCommand(cmdr, argr.ToArray(), map);
            }
            throw new Exception();
        }
        Tuple<CommandObject, IEnumerable<CommandObject>, bool> EvalArg(CommandExpr expr1, VarMap map)
        {
            if (expr1 is CommandCall)
            {
                var expr = (CommandCall)expr1;
                var func = Eval(expr.Command, map);
                return Tuple.Create(func, expr.Argument.Select(x => Eval(x, map)), true);
            }
            var exprr = Eval(expr1, map);
            return Tuple.Create(exprr, new CommandObject[0].AsEnumerable(), (expr1 is CommandString || expr1 is CommandValue)); //IsCallable(exprr, map));//(expr1 is CommandString || expr1 is CommandValue)); //exprr.Type == CommandObjectType.String || exprr.Type == CommandObjectType.Lambda);//(expr1 is CommandString || expr1 is CommandValue));
            throw new Exception();
        }

        private bool IsCallable(CommandObject cmd, VarMap map)
        {
            if (cmd.Type == CommandObjectType.String)
            {
                if (cmd.String == "wrap")
                {
                    return true;
                }
                if (cmd.String == "require")
                {
                    return true;
                }
                if (cmd.String == "eval")
                {
                    return true;
                }
                if (!CommandRegister.CommandList2.ContainsKey(cmd.String))
                {
                    cmd = map.Find(cmd.String);
                    if (cmd.Type != CommandObjectType.String)
                    {
                        return IsCallable(cmd, map);
                    }
                    return false;
                }
                return true;
            }
            else if (cmd.Type == CommandObjectType.Lambda)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        VarMap Map;
        public CommandRunner()
        {
            Map = new VarMap(null);
        }
        public CommandObject Run(string input, string arg)
        {
            var par = new CommandParserEx(input);
            var arguments = arg.Split(',');
            Map.Set("args", new CommandObject(arguments.Select((x) => new CommandObject(x)).ToArray()));
            var expr = par.Expr();
            IsFirstExpr = true;
            var defarg = input.Substring(par.index);
            if (defarg.Length > 0)
            {
                if (defarg[0] == ':')
                    defarg = defarg.Substring(1);
                HasDefaultArg = true;
                DefaultArg = new CommandObject(CommandObjectType.String, defarg);
            }
            return Eval(expr, Map);
        }
    }
    //command lambda
    //lambda(arg(1)+2)
    public class otyaCommand : ICommand
    {
        public string Name
        {
            get
            {
                return "ex";
            }
        }

        public string Process(string msg, string arg)
        {
            return new CommandRunner().Run(msg, arg).ToString();
        }
    }
    public class Gobi : ICommand
    {
        public string Name
        {
            get
            {
                return "gobi";
            }
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
        public string Process(string msg, string arg)
        {
            return gobi(msg, arg);
        }
    }
    public class CommandParser
    {
        public ICommand Command
        {
            get;
            private set;
        }
        public string Argument
        {
            get;
            private set;
        }
        public string Text
        {
            get;
            private set;
        }
        private CommandParser()
        {

        }
        string CommandSubstitution(string text, string a1)
        {
            var args = ParseArguments(Argument);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    i++;
                    if (i >= text.Length)
                    {
                        sb.Append(c);
                        break;
                    }
                    char c2 = text[i];
                    sb.Append(c2);
                    continue;
                }

                if (c == '$')
                {
                    i++;
                    if (i >= text.Length)
                    {
                        sb.Append(c);
                        break;
                    }
                    bool isFunc = false;
                    char c2 = text[i];
                    switch (c2)
                    {
                        case '#':
                            sb.Append(args.Length);
                            break;
                        case '!':
                            sb.Append(a1);
                            break;
                        case '@':
                        case '*':
                            sb.Append(Argument);
                            break;
                        case '&':
                            {
                                if (text.Length > i + 1 && text[i + 1] == '(')
                                {
                                    i++;
                                    isFunc = true;
                                    goto case '(';
                                }
                                break;
                            }
                        case '(':
                            {
                                i++;
                                int oldi = i;
                                int b = 1;
                                for (; i < text.Length; i++)
                                {
                                    char c3 = text[i];
                                    if (c3 == '\\')
                                        continue;
                                    if (c3 == '(')
                                        b++;
                                    if (c3 == ')')
                                    {
                                        b--;
                                        if (b == 0)
                                            break;
                                    }
                                }
                                var command = text.Substring(oldi, i - oldi);
                                if (isFunc)
                                {
                                    //command = CommandSubstitution(command);
                                    if (command.IndexOf(':') == -1)
                                    {
                                        var func = new FuncWrapperCommand(
                                            (msg, arg) =>
                                            {
                                                var cp = ParseCommand(command, true, true);
                                                cp.Argument = arg;
                                                cp.Text = msg;
                                                cp.ExpandVar();
                                                return cp.Command.Process(msg, arg);
                                            }
                                        );
                                        CommandRegister.RegistLocal(func);
                                        sb.Append(func.Name);
                                    }
                                    else
                                    {
                                        var func = new FuncWrapperCommand(
                                            (msg, arg) =>
                                            {
                                                var cp = ParseCommand(command, false, false);
                                                var oldarg = cp.Argument;
                                                cp.Argument = arg;
                                                cp.ExpandVar(msg);
                                                cp.Argument = oldarg;
                                                //cp.ExpandVar();
                                                return cp.Command.Process(cp.Text, cp.Argument);
                                            }
                                        );
                                        CommandRegister.RegistLocal(func);
                                        sb.Append(func.Name);
                                    }
                                }
                                else
                                {
                                    var expandedCommand = CommandSubstitution(command, a1);
                                    /*if (command.IndexOf('=') != -1)
                                    {
                                        //引数あるなら引数部分を展開
                                        var commandarg = CommandSubstitution(command.Substring(command.IndexOf('=')));
                                        command = commandarg + command.Substring(command.IndexOf('='));
                                    }*/
                                    var cp = ParseCommand(expandedCommand, expandedCommand.IndexOf(':') == -1, true);
                                    sb.Append(cp.Command.Process(cp.Text, cp.Argument));
                                }
                            }
                            break;
                        default:
                            if (c2 >= '0' && c2 <= '9')
                            {
                                var oldi = i;
                                while (c2 >= '0' && c2 <= '9' && i < text.Length)
                                {
                                    i++;
                                    if (!(i < text.Length))
                                        break;
                                    c2 = text[i];
                                }
                                var v = int.Parse(text.Substring(oldi, i - oldi));
                                if (v == 0)
                                {
                                    sb.Append(Argument);
                                    continue;
                                }
                                if (v - 1 >= args.Length)
                                {
                                    sb.Append(c);
                                    sb.Append(text[oldi]);
                                    i = oldi;
                                    continue;
                                }
                                sb.Append(args[v - 1]);
                                i--;
                                continue;
                            }
                            sb.Append(c);
                            sb.Append(c2);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
        public void ExpandVar(string a1)
        {
            Text = CommandSubstitution(Text, a1);
        }

        public void ExpandVar()
        {
            ExpandVar("b('ω')v");
            return;
#if false
            var a = ParseArguments(Argument);

            Text = Text.Replace("$#", a.Length.ToString());
            
            Text = Text.Replace("$0", Text);
            for (int j = a.Length; j > 0; j-- )
            {
                Text = Text.Replace("$" + j.ToString(), a[j - 1]);
            }
            Text = Text.Replace("$@", Argument);
            Text = Text.Replace("$*", Argument);
#endif
        }
        public static string[] ParseArguments(string arg)
        {
            return arg.Split(',').Select((x) => new Regex(@"^\s*(.*?)\s*$", RegexOptions.Singleline).Match(x).Groups[1].Value).ToArray();
        }
        public static CommandParser ParseCommand(string m, bool commandonly = false, bool expand = true)
        {
            var regex = new Regex("^(.+?)(=(.*?))?:(.*)$", RegexOptions.Singleline);
            if (commandonly)
            {
                regex = new Regex("^(.+?)(=(.*?))?$", RegexOptions.Singleline);
            }
            var mat = regex.Match(m);
            var command = mat.Groups[1].Value;
            var arg = mat.Groups[3].Value;
            var text = commandonly ? "" : mat.Groups[4].Value;
            var cp = new CommandParser();
            cp.Command = CommandRegister.CommandList[command.Replace("\n", "")];
            cp.Argument = arg;
            cp.Text = text;
            if (expand) cp.ExpandVar();
            return cp;
        }
    }
    public static class CommandRegister
    {
        public static Dictionary<string, ICommand> CommandList;
        public static void RegistLocal(ICommand command)
        {
            Regist(command);
        }
        public static void RegistLocal(ICommand2 command)
        {
            Regist(command);
        }
        public static void Regist(ICommand command)
        {
            if (CommandList == null)
            {
                CommandList = new Dictionary<string, ICommand>();
            }
            CommandList.Add(command.Name, command);
            if (CommandList2 == null || !CommandList2.ContainsKey(command.Name))
                Regist(new OldCommandWrapper(command));
        }
        public static Dictionary<string, ICommand2> CommandList2;
        public static void Regist(ICommand2 command)
        {
            if (CommandList2 == null)
            {
                CommandList2 = new Dictionary<string, ICommand2>();
            }
            CommandList2.Add(command.Name, command);
        }
    }
    public class OldCommandWrapper : ICommand2
    {
        ICommand Command;
        public OldCommandWrapper(ICommand comm)
        {
            Command = comm;
        }
        public string Name
        {
            get
            {
                return Command.Name;
            }
        }
        public CommandObject Process(CommandObject[] arg)
        {
            if (arg.Length == 0)
            {
                return new CommandObject(CommandObjectType.String, Command.Process("", ""));
            }
            return new CommandObject(CommandObjectType.String, Command.Process(arg.First().ToString(), string.Join(",", arg.Skip(1))));/*
                arg.Length > 1 ? arg.Skip(1)//Take(arg.Length - 1)
                .Select(x => x.ToString())
                .Aggregate((x, y) => y + "," + x) : ""));*/
        }
    }
    public class EqualCommand2 : ICommand2
    {
        public string Name
        {
            get
            {
                return "equal";
            }
        }
        public CommandObject Process(CommandObject[] arg)
        {
            if (arg[0] == arg[1])
                return new CommandObject("true");
            else
                return new CommandObject("false");
        }
    }
    public class EchoCommand2 : ICommand2
    {
        public string Name
        {
            get
            {
                return "echo";
            }
        }
        public CommandObject Process(CommandObject[] arg)
        {
            return arg[0];
        }
    }
    public class Array : ICommand2
    {
        public string Name
        {
            get
            {
                return "array";
            }
        }
        public CommandObject Process(CommandObject[] arg)
        {
            return new CommandObject(arg);
        }
    }
    public class Array2 : ICommand2
    {
        public string Name
        {
            get
            {
                return "array2";
            }
        }
        public CommandObject Process(CommandObject[] arg)
        {
            return new CommandObject(new CommandObject[Convert.ToInt32(arg[0].String)]);
        }
    }
    public class ArrayAccess : ICommand2
    {
        public string Name
        {
            get
            {
                return "arrayaccess";
            }
        }
        public CommandObject Process(CommandObject[] arg)
        {
            if (arg.Length <= 2)
            {
                return arg[0].Array[Convert.ToInt32(arg[1].String)];
            }
            else
            {
                return arg[0].Array[Convert.ToInt32(arg[1].String)] = arg[2];
            }
        }
    }
    public class ArrayItem : ICommand2
    {
        public string Name
        {
            get
            {
                return "arrayitem";
            }
        }
        public CommandObject Process(CommandObject[] arg)
        {
            if (arg.Length <= 2)
            {
                return arg[1].Array[Convert.ToInt32(arg[0].String)];
            }
            else
            {
                return arg[2].Array[Convert.ToInt32(arg[0].String)] = arg[1];
            }
        }
    }
    public class StrLen : ICommand2
    {
        public string Name
        {
            get
            {
                return "strlen";
            }
        }
        public CommandObject Process(CommandObject[] arg)
        {
            if (arg[0].Type == CommandObjectType.String)
            {
                return new CommandObject(arg[0].String.Length.ToString());
            }
            throw new Exception();
        }
    }
    public class ArrayLen : ICommand2
    {
        public string Name
        {
            get
            {
                return "arylen";
            }
        }
        public CommandObject Process(CommandObject[] arg)
        {
            if (arg[0].Type == CommandObjectType.Array)
            {
                return new CommandObject(arg[0].Array.Length.ToString());
            }
            throw new Exception();
        }
    }
#if MECAB
    public class SuperMecab : ICommand
    {
        public string Name
        {
            get
            {
                return "supermecab";
            }
        }

        public string Process(string msg, string arg)
        {
            var m = Mecaber.Mecab(msg);
            var jarray = new JArray();
            while (m != null)
            {
                string f = null;
                try
                {
                    f = m.Feature;
                }
                catch (NullReferenceException)
                { }
                jarray.Add(new JObject { { "Alpha", m.Alpha }, { "Beta", m.Beta }, { "BPos", m.BPos }, { "CharType", m.CharType }, { "Cost", m.Cost },
                    {"EPos", m.EPos }, {"Feature", f == null ? "" : f },{"IsBest", m.IsBest},
{"LCAttr", m.LCAttr},
{"Length", m.Length},
{"PosId", m.PosId},
{"Prob", m.Prob},
{"RCAttr", m.RCAttr},
{"RLength", m.RLength},
{"Stat", m.Stat.ToString()},
{"Surface", m.Surface},
{"WCost", m.WCost} });
                m = m.Next;
            }
            return jarray.ToString();
        }
    }
#endif
    public class Json : ICommand2
    {
        string ICommand2.Name
        {
            get
            {
                return "json";
            }
        }
        CommandObject Convert(JObject jt)
        {
            var vm = new VarMap(null);
            var lam = new CommandLambdaObject(null, vm, null, null);
            var root = new CommandObject(CommandObjectType.Lambda, lam);
            foreach (var item in jt)
            {
                vm.Map.Add(item.Key, Convert(item.Value));
            }
            return root;
        }
        CommandObject Convert(JToken jt)
        {
            switch (jt.Type)
            {
                case JTokenType.None:
                    return new CommandObject();
                case JTokenType.Object:
                    return Convert((JObject)jt);
                case JTokenType.Array:
                    return new CommandObject(CommandObjectType.Array, (((JArray)jt).Select((x) => Convert(x)).ToArray()));
                case JTokenType.Constructor:
                    return new CommandObject();
                case JTokenType.Property:
                    return new CommandObject();
                case JTokenType.Comment:
                    return new CommandObject();
                case JTokenType.Integer:
                    return new CommandObject(((JValue)jt).Value.ToString());
                case JTokenType.Float:
                    return new CommandObject(((JValue)jt).Value.ToString());
                case JTokenType.String:
                    return new CommandObject(((JValue)jt).Value.ToString());
                case JTokenType.Boolean:
                    return new CommandObject(((JValue)jt).Value.ToString());
                case JTokenType.Null:
                    return new CommandObject();
                case JTokenType.Undefined:
                    return new CommandObject();
                case JTokenType.Date:
                    return new CommandObject();
                case JTokenType.Raw:
                    return new CommandObject();
                case JTokenType.Bytes:
                    return new CommandObject();
                case JTokenType.Guid:
                    return new CommandObject();
                case JTokenType.Uri:
                    return new CommandObject();
                case JTokenType.TimeSpan:
                    return new CommandObject();
                default:
                    return new CommandObject();
            }

        }

        CommandObject ICommand2.Process(CommandObject[] arg)
        {
            var json = arg[0].String;
            var j = JToken.Parse(json);
            return Convert(j);
        }
    }
    class RegexCommand2 : ICommand2
    {
        public string Name
        {
            get
            {
                return "regex";
            }
        }

        public CommandObject Process(CommandObject[] arg)
        {
            var pattern = arg[0].ToString();
            var regex = new Regex(pattern);
            return new CommandObject(CommandObjectType.Object, regex);
            throw new NotImplementedException();
        }
    }
    class RegexSplit : ICommand2
    {
        public string Name
        {
            get
            {
                return "splitregex";
            }
        }

        public CommandObject Process(CommandObject[] arg)
        {
            if (arg[0].Object is Regex)
            {
                Regex regex = arg[0].Object as Regex;
                return new CommandObject(regex.Split(arg[0].ToString()).Select(x => new CommandObject(x)).ToArray());
            }
            return new CommandObject();
        }
    }


    public interface ICommand
    {
        string Name { get; }
        string Process(string msg, string arg);
    }
    class Echo : ICommand
    {
        public string Name
        {
            get
            {
                return "echo";
            }
        }
        public string Process(string msg, string arg)
        {
            return msg;
        }
    }
    class AliasDefine : ICommand
    {
        public static string AliasFile = "aliases.json";
        public static Dictionary<string, AliasCommand> AliasList = new Dictionary<string, AliasCommand>();
        public static bool NewAlias(string name, string command)
        {
            var cmd = new AliasCommand(name, command);
            if (AliasList.ContainsKey(name))
            {
                AliasList[name] = cmd;
            }
            else
            {
                AliasList.Add(name, cmd);
            }
            if (CommandRegister.CommandList2.ContainsKey(name))
            {
                var oc = CommandRegister.CommandList[name];
                if (oc is AliasCommand)
                {
                    CommandRegister.CommandList[name] = cmd;
                    CommandRegister.CommandList2[name] = new OldCommandWrapper(cmd);
                }
                else return false;
            }
            else
            {
                CommandRegister.CommandList.Add(name, cmd);
                CommandRegister.CommandList2.Add(name, new OldCommandWrapper(cmd));
            }
            return true;
        }
        public static void LoadAlias()
        {
            if (!System.IO.File.Exists(AliasFile))
            {
                System.IO.File.WriteAllText(AliasFile, "{}");
            }
            var j = JsonConvert.DeserializeObject<Dictionary<string, AliasCommand>>(System.IO.File.ReadAllText(AliasFile));
            foreach (var i in j)
            {
                if (!NewAlias(i.Value.Name, i.Value.Command)) return;
            }
        }
        public static void AddAlias(string name, string command)
        {
            lock (CommandRegister.CommandList)
            {
                if (!NewAlias(name, command)) return;
                var j = JsonConvert.SerializeObject(AliasList);
                System.IO.File.WriteAllText(AliasFile, j);
            }
            return;
        }
        public string Name
        {
            get
            {
                return "alias";
            }
        }
        public string Process(string msg, string arg)
        {
            return AliasList[msg].Name + "=" + AliasList[msg].Command;
        }
    }
    class AliasCommand : ICommand
    {
        public AliasCommand(string name, string command)
        {
            Name = name;
            Command = command;
        }
        public string Command
        {
            get;
            set;
        }
        public string Name
        {
            get;
            set;
        }
        [ThreadStatic]
        public static int RecCount = 0;
        public string Process(string msg, string arg)
        {
            if (RecCount >= 128) return "";
            RecCount++;
            var Command = this.Command.Replace("$0", msg);
            if (-1 != Command.IndexOf(':'))
            {
                var ret = CommandParser.ParseCommand(Command);//CommandRegister.CommandList[Name].Process(Command, Argument);
                if (ret.Argument == "")
                {
                    var t = CommandParser.ParseCommand(Command, false, false).Text;
                    var ab = CommandParser.ParseCommand(ret.Command.Name + "=" + arg + ":" + t);
                    return /*ret*/ab.Command.Process(ab.Text/* + msg*/, arg);
                }
                return ret.Command.Process(ret.Text + msg, ret.Argument);
            }

            var ret2 = CommandParser.ParseCommand(Command, true);
            if (ret2.Argument == "")
            {
                return ret2.Command.Process(msg, arg);
            }
            return ret2.Command.Process(msg, ret2.Argument);
        }
    }
    class Filter : ICommand
    {
        private string m_name;
        private Func<string, string> m_func;
        public Filter(string name, Func<string, string> func)
        {
            m_name = name;
            m_func = func;
        }
        public string Name
        {
            get
            {
                return m_name;
            }
        }
        public string Process(string msg, string arg)
        {
            return m_func(msg);
        }
    }
#if RAKUDALANG
    class Rakuda : ICommand
    {
        public string Name
        {
            get
            {
                return "rakuda";
            }
        }
        public string Process(string msg, string arg)
        {
            var rakuda = msg;
            rakuda = rakuda.Replace(":camel:", "🐫");
            rakuda = rakuda.Replace(":point_right:", "👉");//:last_quarter_moon_with_face: :first_quarter_moon_with_face:
            rakuda = rakuda.Replace(":last_quarter_moon_with_face:", "🌜");
            rakuda = rakuda.Replace(":first_quarter_moon_with_face:", "🌛");
            rakuda = rakuda.Replace(":full_moon_with_face", "🌝");
            rakuda = rakuda.Replace(":sun_with_face", "🌞");
            rakuda = rakuda.Replace(":", " ");
            rakuda = rakuda.Replace("d-man", ":");
            var text = "";
            try
            {
                var ret = otya.Rakuda.RunExpression(rakuda);
                if (ret.Item1[0] == 'F')
                {
                    text = "```" + ret.Item1 + "\r\n```";
                }
                else
                    text = "```" + ret.Item4.ToString() + "\r\nresult:" + ret.Item3.ToString() + "\r\n```";
            }
            catch (KeyNotFoundException e)
            {
                text = "```存在しない変数\r\n```";
            }
            catch (otya.Rakuda.AssertionFailureException e)
            {
                text = "```AssertionFailure\r\n```";
            }
            catch (otya.Rakuda.NotDefinedVar e)
            {
                text = "```" + e.Data0 + " is not defined.```";
            }
            catch (otya.Rakuda.DefinedVar e)
            {
                text = "```" + e.Data0 + " is defined.```";
            }
            catch
            {
                text = "```fail\r\n```";
            }
            return text;
        }
    }
#endif
    class Repeat : ICommand
    {
        public string Name
        {
            get
            {
                return "repeat";
            }
        }
        public string Process(string msg, string arg)
        {
            var a = arg.Split(',');
            var args = a.Select((x) => new Regex(@"^\s*(.*?)\s*$").Match(x).Groups[1].Value).ToArray();
            int count = 1;
            if (args.Length >= 1)
            {
                count = Math.Min(Convert.ToInt32(args[0]), 10);
            }
            if (args.Length >= 2)
            {
                var cmd = CommandParser.ParseCommand(arg.Substring(a[0].Length + 1), true);
                for (int i = 0; i < count; i++)
                {
                    msg = cmd.Command.Process(msg, cmd.Argument);
                }
                return msg;
            }
            return (new System.Text.StringBuilder()).Insert(0, msg, count).ToString();
        }
    }
    class Pipe : ICommand
    {
        public string Name
        {
            get
            {
                return "pipe";
            }
        }
        //pipe:echo|nmoudame:
        public string Process(string msg, string arg)
        {
            var i = msg.IndexOf(':');
            string command, text;
            if (i == -1)
            {
                command = msg;
                text = "";
            }
            else
            {
                command = msg.Substring(0, i);
                text = msg.Substring(i + 1);
            }
            var commands = command.Split('|');

            var mat = commands
                .Select((x) => new Regex(@"^\s*(.*?)\s*$").Match(x).Groups[1].Value)
                .Aggregate(text, (x, y) =>
                    {
                        var cmd = CommandParser.ParseCommand(y, true);
                        return cmd.Command.Process(x, cmd.Argument);
                    });
            Console.WriteLine(mat);
            return mat;
        }
    }
    static class CommandUtil
    {
        static public double ToDouble(string m)
        {
            if (m != "")
                return Convert.ToDouble(m);
            return 0;
        }
    }
    class Plus : ICommand
    {
        public string Name
        {
            get
            {
                return "plus";
            }
        }
        public string Process(string msg, string arg)
        {
            return (CommandUtil.ToDouble(msg) + CommandParser.ParseArguments(arg).Select((x) => CommandUtil.ToDouble(x)).Aggregate((x, y) => x + y)).ToString();
        }
    }
    class Minus : ICommand
    {
        public string Name
        {
            get
            {
                return "minus";
            }
        }
        public string Process(string msg, string arg)
        {
            return (CommandUtil.ToDouble(msg) - CommandParser.ParseArguments(arg).Select((x) => CommandUtil.ToDouble(x)).Aggregate((x, y) => x - y)).ToString();
        }
    }
    class Mul : ICommand
    {
        public string Name
        {
            get
            {
                return "mul";
            }
        }
        public string Process(string msg, string arg)
        {
            return (CommandUtil.ToDouble(msg) * CommandParser.ParseArguments(arg).Select((x) => CommandUtil.ToDouble(x)).Aggregate((x, y) => x * y)).ToString();
        }
    }
    class Div : ICommand
    {
        public string Name
        {
            get
            {
                return "div";
            }
        }
        public string Process(string msg, string arg)
        {
            return (CommandUtil.ToDouble(msg) / CommandParser.ParseArguments(arg).Select((x) => CommandUtil.ToDouble(x)).Aggregate((x, y) => x / y)).ToString();
        }
    }
    class Mod : ICommand
    {
        public string Name
        {
            get
            {
                return "mod";
            }
        }
        public string Process(string msg, string arg)
        {
            return (CommandUtil.ToDouble(msg) % CommandParser.ParseArguments(arg).Select((x) => CommandUtil.ToDouble(x)).Aggregate((x, y) => x % y)).ToString();
        }
    }
    class Replace : ICommand
    {
        public string Name
        {
            get
            {
                return "replace";
            }
        }
        public string Process(string msg, string arg)
        {
            var r = CommandParser.ParseArguments(arg);
            return msg.Replace(r[0], r[1]);
        }
    }
    class Append : ICommand
    {
        public string Name
        {
            get
            {
                return "append";
            }
        }
        public string Process(string msg, string arg)
        {
            return msg + arg;
        }
    }
    class Equal : ICommand
    {
        public string Name
        {
            get
            {
                return "equal";
            }
        }
        public string Process(string msg, string arg)
        {
            return msg == arg ? "true" : "false";
        }
    }
    class Kao : ICommand
    {
        string ICommand.Name
        {
            get { return "kao"; }
        }

        public static string kao(string kaoo)
        {
            string kani = "'ω'";
            var result = kaoo.Replace("^o^", kani).Replace("'o'", kani).Replace("´◓ω◔`", kani).Replace("* ՞ਊ՞ *", kani)
                .Replace("ε:", "ω'").Replace(":3", "'ω").Replace("ε:", "ω'").Replace("'。'", kani).Replace("´◓ｑ◔｀", kani)
                .Replace("◡⁀◡", kani).Replace("^ω^", kani).Replace("・◡・", kani);
            var regex = new Regex(@"‿|◡|ਊ|ټ|౪|▿|‸|☋|∀|ڼ|۝|☢|◊|ཀ|⊖|ε|௰|﹏");
            result = regex.Replace(result, "ω");
            regex = new Regex(@"’|´|｀|◝|^|＾|՞|◔|‾|՞|◜|◞|◟|へ|՞|⊙|Ò|Ó|⁰|˘|í|ì|✹|ة|◠|~|･|`");
            result = regex.Replace(result, "'");
            result = result.Replace("''", "'").Replace("''", "'");
            return result;
        }
        string ICommand.Process(string msg, string arg)
        {
            return kao(msg);
        }
    }
    class CommandRandom : ICommand
    {
        [ThreadStatic]
        static Random random;
        public string Name
        {
            get
            {
                return "random";
            }
        }
        public string Process(string msg, string arg)
        {
            if (random == null)
                random = new Random();
            return random.Next(Convert.ToInt32(msg)).ToString();
        }
    }
    class CommandSubstr : ICommand
    {
        public string Name
        {
            get
            {
                return "substr";
            }
        }
        public string Process(string msg, string arg)
        {
            var r = CommandParser.ParseArguments(arg);
            return msg.Substring(Convert.ToInt32(r[0]), Convert.ToInt32(r[1]));
        }
    }
    class CommandChr : ICommand
    {
        public string Name
        {
            get
            {
                return "chr";
            }
        }
        public string Process(string msg, string arg)
        {
            return ((char)Convert.ToInt32(msg)).ToString();
        }
    }
    class CommandLen : ICommand
    {
        public string Name
        {
            get
            {
                return "len";
            }
        }
        public string Process(string msg, string arg)
        {
            return msg.Length.ToString();
        }
    }

    class If : ICommand
    {
        public string Name
        {
            get
            {
                return "if";
            }
        }
        public string Process(string msg, string arg)
        {
            var args = CommandParser.ParseArguments(arg);
            if (args.Length == 0)
                return "";
            if (msg == "true")
            {
                return args[0];
            }
            if (CommandRegister.CommandList.ContainsKey(msg))
            {
                var com = CommandRegister.CommandList[msg];
                msg = com.Process("", "");
            }
            double a;
            if (double.TryParse(msg, out a) && a != 0)
            {
                return args[0];
            }
            return args[1];
        }
    }
    class HasLocal : ICommand
    {
        public string Name
        {
            get
            {
                return "haslocal";
            }
        }
        public string Process(string msg, string arg)
        {
            var variable = CallContext.LogicalGetData($"{nameof(RakudaSlack)}.Variable") as Dictionary<string, object>;
            if (variable == null)
            {
                return "";
            }
            if (!variable.ContainsKey(msg))
                return "false";
            return "true";
        }
    }
    class GetLocal : ICommand
    {
        public string Name
        {
            get
            {
                return "getlocal";
            }
        }
        public string Process(string msg, string arg)
        {
            var variable = CallContext.LogicalGetData($"{nameof(RakudaSlack)}.Variable") as Dictionary<string, object>;
            if (variable == null)
            {
                return "";
            }
            if (!variable.ContainsKey(msg))
                return "";
            return variable[msg].ToString();
        }
    }
    class SetLocal : ICommand
    {
        public string Name
        {
            get
            {
                return "setlocal";
            }
        }
        public string Process(string msg, string arg)
        {
            var variable = CallContext.LogicalGetData($"{nameof(RakudaSlack)}.Variable") as Dictionary<string, object>;
            if (variable == null)
            {
                variable = new Dictionary<string, object>();
                CallContext.LogicalSetData($"{nameof(RakudaSlack)}.Variable", variable);
            }
            return (variable[arg] = msg) as string;
        }
    }
    class WGet : ICommand
    {
        public string Name
        {
            get
            {
                return "wget";
            }
        }

        public string Process(string msg, string arg)
        {
            var allowed = new Regex("(^" + Regex.Escape("http://tekito.kanichat.com/nmoudame/response.php") + ")" + "|"
                + "(^" + Regex.Escape("http://ja.wikipedia.org/w/api.php?") + ")");
            var nmdmsg = msg.Replace("<", "").Replace(">", "");
            if (allowed.IsMatch(nmdmsg))
            {
                return new WebClient().DownloadString(nmdmsg);
            }
            else
            {
                return "<MARQUEE><BLINK>&lt;たしv('ω')vチャット</MARQUEE>";
            }
        }
    }
    static class RegexCommand
    {

        private static Dictionary<string, RegexOptions> OptionTable = new Dictionary<string, RegexOptions>
        {
            { "ignorecase", RegexOptions.IgnoreCase },
            { "multiline", RegexOptions.Multiline },
            { "explicitcapture", RegexOptions.ExplicitCapture },
            {"compiled", RegexOptions.Compiled },
            {"singleline", RegexOptions.Singleline },
            {"ignorepatternwhitespace", RegexOptions.IgnorePatternWhitespace },
            {"righttoleft", RegexOptions.RightToLeft },
            {"ecmascript", RegexOptions.ECMAScript },
            {"cultureinvariant", RegexOptions.CultureInvariant },
        };

        public static Regex MakeRegexInstance(string pattern)
        {
            return MakeRegexInstance(pattern, "");
        }
        public static Regex MakeRegexInstance(string pattern, string option)
        {
            Regex regex = null;
            var options = option.Split('|');
            var aggregatedoption = options.Select(x => OptionTable.ContainsKey(x) ? OptionTable[x] : RegexOptions.None)
                .Aggregate((x, y) => x | y);
            regex = new Regex(pattern, aggregatedoption);
            return regex;
        }
    }
    class RegexReplace : ICommand
    {
        public string Name
        {
            get
            {
                return "replaceregex";
            }
        }

        public string Process(string msg, string arg)
        {
            var args = arg.Split(',');
            Regex regex = null;
            if (args.Length > 2)
            {
                regex = RegexCommand.MakeRegexInstance(args[0], args[2]);
            }
            else
            {
                regex = RegexCommand.MakeRegexInstance(args[0]);
            }
            return regex.Replace(msg, args[1]);
        }
    }

    class RegexIsMatch : ICommand
    {
        public string Name
        {
            get
            {
                return "ismatchregex";
            }
        }

        public string Process(string msg, string arg)
        {
            var args = arg.Split(',');
            Regex regex = null;
            if (args.Length > 1)
            {
                regex = RegexCommand.MakeRegexInstance(args[0], args[1]);
            }
            else
            {
                regex = RegexCommand.MakeRegexInstance(args[0]);
            }
            return regex.IsMatch(msg) ? "true" : "false";
        }
    }

    class Browser : ICommand
    {
        public string Name
        {
            get
            {
                return "allbrowser";
            }
        }

        public string Process(string msg, string arg)
        {
            msg = new Regex("<(.+?)(\\|.+?)?>").Replace(msg, "$1");
            if (!msg.StartsWith("https://") && !msg.StartsWith("http://"))
                return "";
            msg = msg.Replace("^", Uri.EscapeDataString("^")).Replace("\"", Uri.EscapeDataString("\"")).Replace("\\", Uri.EscapeDataString("\\")).Replace("&", Uri.EscapeDataString("&"));
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "w3m.exe";
            p.StartInfo.Arguments = "-dump \"" + msg + "\"";
            p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            p.Start();
            string results = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            p.Close();
            return results;
        }
    }

    
    class OnReaction : ICommand
    {
        public string Name
        {
            get
            {
                return "onreaction";
            }
        }

        public string Process(string msg, string arg)
        {
            var slack = Slack.Current;
            var cmsg = Slack.CurrentMessage;
            var command = CommandParser.ParseCommand(msg, true);

            slack.OnReaction += (sender, reaction) =>
            {
                if (reaction.type != "reaction_added")
                    return;
                if (reaction.user == slack.User)
                    return;
                Task.Run(() =>
                {
                    Slack.CurrentMessage = cmsg;
                    Slack.Current = slack;
                    JToken ts;
                    if (reaction.item.TryGetValue("ts", out ts) && ts.Type == JTokenType.String && ts.Value<string>() == cmsg.PostedMessage.ts)
                    {
                        cmsg.Text = command.Command.Process(reaction.reaction, slack.GetUserName(reaction.user));
                        cmsg.Post();
                    }
                });
            };
            return "success";
        }
    }
    class OnReactionRemoved : ICommand
    {
        public string Name
        {
            get
            {
                return "onreactionremoved";
            }
        }

        public string Process(string msg, string arg)
        {
            var slack = Slack.Current;
            var cmsg = Slack.CurrentMessage;
            var command = CommandParser.ParseCommand(msg, true);

            slack.OnReaction += (sender, reaction) =>
            {
                if (reaction.type != "reaction_removed")
                    return;
                if (reaction.user == slack.User)
                    return;
                Task.Run(() =>
                {
                    Slack.CurrentMessage = cmsg;
                    Slack.Current = slack;
                    JToken ts;
                    if (reaction.item.TryGetValue("ts", out ts) && ts.Type == JTokenType.String && ts.Value<string>() == cmsg.PostedMessage.ts)
                    {
                        cmsg.Text = command.Command.Process(reaction.reaction, slack.GetUserName(reaction.user));
                        cmsg.Post();
                    }
                });
            };
            return "success";
        }
    }
    class OnPosted : ICommand
    {
        public string Name
        {
            get
            {
                return "onposted";
            }
        }

        public string Process(string msg, string arg)
        {
            var slack = Slack.Current;
            var cmsg = Slack.CurrentMessage;
            var command = CommandParser.ParseCommand(msg, true);

            cmsg.OnPosted += () =>
            {
                Task.Run(() =>
                {
                    Slack.CurrentMessage = cmsg;
                    Slack.Current = slack;
                    command.Command.Process("", command.Argument);
                });
            };
            return "success";
        }
    }
    class AddReaction : ICommand
    {
        public string Name
        {
            get
            {
                return "addreaction";
            }
        }

        public string Process(string msg, string arg)
        {
            var slack = Slack.Current;
            var cmsg = Slack.CurrentMessage;
            cmsg.AddReaction(msg);
            return "success";
        }
    }
    class SetIconEmoji : ICommand
    {
        public string Name
        {
            get
            {
                return "seticonemoji";
            }
        }

        public string Process(string msg, string arg)
        {
            Slack.CurrentMessage.IconEmoji = msg;
            return msg;
        }
    }
}
