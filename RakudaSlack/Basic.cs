using System;
using RakudaSlack;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StringArray = System.Collections.Generic.List<string>;
using NumberArray = System.Collections.Generic.List<double>;

namespace BASIC
{
    class BASIC : ICommand
    {
        public string Name
        {
            get
            {
                return "basic";
            }
        }

        public string Process(string msg, string arg)
        {
            try
            {
                var i = new Interpreter(msg);
                return i.Run();
            }
            catch(BasicException e)
            {
                return e.Message;
            }
        }
    }
    [System.Serializable]
    public class BasicException : Exception
    {
        public BasicException() { }
        public BasicException(string message) : base(message) { }
        public BasicException(string message, Exception inner) : base(message, inner) { }
        protected BasicException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    enum TokenType
    {
        Print,
        Iden,
        Number,
        NewLine,
        Plus,
        Minus,
        Mul,
        Div,
        Mod,
        String,
        Semicolon,
        Comma,
        Colon,
        Next,
        For,
        To,
        Equal,
        Label,
        Goto,
        Gosub,
        Return,
        If,
        Then,
        Let,
        LParen,
        RParen,
        Greater,
        GreaterEq,
        LessEq,
        Less,
        NotEqual,
        LBracket,
        RBracket,
        MakeProcInstance,
    }
    struct Token
    {
        public TokenType Type;
        public double Number;
        public string String;
        public Token(double number)
        {
            Type = TokenType.Number;
            String = null;
            Number = number;
        }
        public Token(TokenType t, string number)
        {
            Type = t;
            String = number;
            Number = 0;
        }
        public Token(TokenType t)
        {
            Type = t;
            String = null;
            Number = 0;
        }
    }
    enum ValueType
    {
        None,
        Number,
        String,
        StringArray,
        NumberArray,
    }
    struct Value
    {
        public ValueType Type;
        double mnumber;
        object mobject;

        public string String
        {
            get
            {
                if (Type != ValueType.String)
                    throw new BasicException("Type mismatch");
                return mobject as string;
            }
        }
        public double Number
        {
            get
            {
                if (Type != ValueType.Number)
                    throw new BasicException("Type mismatch");
                return mnumber;
            }
        }
        public Value this[int i]
        {
            get
            {
                switch (Type)
                {
                    case ValueType.None:
                    case ValueType.Number:
                    case ValueType.String:
                    default:
                        throw new BasicException("Type mismatch");
                    case ValueType.StringArray:
                        return new Value((mobject as StringArray)[i]);
                    case ValueType.NumberArray:
                        return new Value((mobject as NumberArray)[i]);
                }
            }
            set
            {
                switch (Type)
                {
                    case ValueType.None:
                    case ValueType.Number:
                    case ValueType.String:
                    default:
                        throw new BasicException("Type mismatch");
                    case ValueType.StringArray:
                        (mobject as StringArray)[i] = value.String;
                        break;
                    case ValueType.NumberArray:
                        (mobject as NumberArray)[i] = value.Number;
                        break;
                }
            }
        }


        public Type CLRType
        {
            get
            {
                switch (Type)
                {
                    case ValueType.Number:
                        return typeof(double);
                    case ValueType.String:
                        return typeof(string);
                    default:
                        throw new BasicException("Type mismatch");
                }
            }
        }
        public object Object
        {
            get
            {
                switch (Type)
                {
                    case ValueType.Number:
                        return Number;
                    case ValueType.String:
                        return String;
                    case ValueType.NumberArray:
                    case ValueType.StringArray:
                        return mobject;
                    default:
                        throw new BasicException("Type mismatch");
                }
            }
        }

        public Value(double number)
        {
            Type = ValueType.Number;
            mobject = null;
            mnumber = number;
        }
        public Value(ValueType vt) : this()
        {
            Type = vt;
        }
        public Value(object obj) : this()
        {
            if (obj as double? != null)
            {
                Type = ValueType.Number;
                mnumber = (double)obj;
                return;
            }
            if (obj is string)
            {
                Type = ValueType.String;
                mobject = obj;
                return;
            }
            if (obj is StringArray)
            {
                Type = ValueType.StringArray;
                mobject = obj;
                return;
            }
            if (obj is NumberArray)
            {
                Type = ValueType.NumberArray;
                mobject = obj;
                return;
            }
            throw new BasicException("Type mismatch");
        }
        public Value(string number)
        {
            Type = ValueType.String;
            mobject = number;
            mnumber = 0;
        }

        public Value(bool v) : this()
        {
            Type = ValueType.Number;
            this.mnumber = v ? 1 : 0;
        }
        public Value(StringArray number) : this()
        {
            Type = ValueType.StringArray;
            mobject = number;
        }
        public Value(NumberArray number) : this()
        {
            Type = ValueType.NumberArray;
            mobject = number;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case ValueType.Number:
                    return Number.ToString();
                case ValueType.String:
                    return String as string;
                default:
                    throw new BasicException("Type mismatch");
            }
        }
    }


    class Interpreter
    {
        private string Program;
        BuiltinFunction bf = new BuiltinFunction();
        Dictionary<string, Value> variable = new Dictionary<string, Value>();
        Dictionary<string, int> labelTable = new Dictionary<string, int>();

        public Interpreter(string msg)
        {
            this.Program = msg;
        }
        public class Interpreter2
        {
            internal string Program;
            internal BuiltinFunction bf;
            internal Dictionary<string, Value> variable;
            bool IsIdentFirstChar(char c)
            {
                return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
            }
            bool IsIdentChar(char c)
            {
                return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || (c >= '0' && c <= '9');
            }
            bool p;
            Token m_current;
            Token Current
            {
                get
                {
                    if (!p)
                        Next();
                    return m_current;
                }
            }

            public Interpreter Interpreter { get; set; }

            void Next()
            {
                m_current = NextInternal();
                p = true;
            }
            Token NextInternal()
            {
                for (; i < Program.Length; i++)
                {
                    var c = Program[i];
                    if (c == ' ')
                        continue;
                    i++;
                    switch (c)
                    {
                        case '+':
                            return new Token(TokenType.Plus);
                        case '-':
                            return new Token(TokenType.Minus);
                        case '*':
                            return new Token(TokenType.Mul);
                        case '/':
                            return new Token(TokenType.Div);
                        case '%':
                            return new Token(TokenType.Mod);
                        case '(':
                            return new Token(TokenType.LParen);
                        case ')':
                            return new Token(TokenType.RParen);
                        case ';':
                            return new Token(TokenType.Semicolon);
                        case ':':
                            return new Token(TokenType.Colon);
                        case '?':
                            return new Token(TokenType.Print);
                        case ',':
                            return new Token(TokenType.Comma);
                        case '[':
                            return new Token(TokenType.LBracket);
                        case ']':
                            return new Token(TokenType.RBracket);
                        default:
                            i--;
                            break;
                    }
                    if (c == '=')
                    {
                        i++;
                        return new Token(TokenType.Equal);
                    }
                    if (c == '<')
                    {
                        i++;
                        if (Program[i] == '>')
                        {
                            i++;
                            return new Token(TokenType.NotEqual);
                        }
                        if (Program[i] == '=')
                        {
                            i++;
                            return new Token(TokenType.LessEq);
                        }
                        return new Token(TokenType.Less);
                    }
                    if (c == '>')
                    {
                        i++;
                        if (Program[i] == '<')
                        {
                            i++;
                            return new Token(TokenType.NotEqual);
                        }
                        if (Program[i] == '=')
                        {
                            i++;
                            return new Token(TokenType.GreaterEq);
                        }
                        return new Token(TokenType.Greater);
                    }
                    if (c == '"')
                    {
                        int s = i;
                        for (i++; i < Program.Length; i++)
                        {
                            c = Program[i];
                            if (c == '"')
                            {
                                break;
                            }
                        }
                        i++;
                        return new Token(TokenType.String, Program.Substring(s + 1, i - s - 2));
                    }
                    if (c == '@' || IsIdentFirstChar(c))
                    {
                        bool islabel = c == '@';
                        int s = i;
                        if (islabel)
                            i++;
                        for (; i < Program.Length; i++)
                        {
                            c = Program[i];
                            if (!IsIdentChar(c))
                            {
                                if (c == '$')
                                    i++;
                                break;
                            }
                        }
                        var v = Program.Substring(s, i - s).ToUpper();
                        if (islabel)
                            return new Token(TokenType.Label, v);
                        switch (v)
                        {
                            case "PRINT":
                                return new Token(TokenType.Print, v);
                            case "FOR":
                                return new Token(TokenType.For, v);
                            case "NEXT":
                                return new Token(TokenType.Next, v);
                            case "TO":
                                return new Token(TokenType.To, v);
                            case "GOTO":
                                return new Token(TokenType.Goto, v);
                            case "GOSUB":
                                return new Token(TokenType.Gosub, v);
                            case "RETURN":
                                return new Token(TokenType.Return, v);
                            case "IF":
                                return new Token(TokenType.If, v);
                            case "THEN":
                                return new Token(TokenType.Then, v);
                            case "LET":
                                return new Token(TokenType.Let, v);
                            case "MAKEPROCINSTANCE":
                                return new Token(TokenType.MakeProcInstance, v);
                            default:
                                return new Token(TokenType.Iden, v);
                        }
                    }
                    if (char.IsDigit(c))
                    {
                        int s = i;
                        for (; i < Program.Length; i++)
                        {
                            c = Program[i];
                            if (!char.IsDigit(c))
                            {
                                break;
                            }
                        }
                        return new Token(int.Parse(Program.Substring(s, i - s)));
                    }
                    if (c == '\n')
                    {
                        i++;
                        return new Token(TokenType.NewLine);
                    }
                    throw new BasicException("unknown char");
                }
                return new Token(TokenType.NewLine);
            }
            StringBuilder result;
            public Value Term()
            {
                var current = Current;
                Next();
                switch (current.Type)
                {
                    case TokenType.Iden:
                        {
                            if (Current.Type == TokenType.LParen)
                            {
                                List<Value> args = new List<Value>();
                                Next();
                                if (Current.Type == TokenType.RParen)
                                    Next();
                                else while (Current.Type != TokenType.RParen)
                                {
                                    args.Add(Expr());
                                    if (Current.Type == TokenType.Comma)
                                    {
                                        Next();
                                        continue;
                                    }
                                    if (Current.Type != TokenType.RParen)
                                    {
                                        throw new BasicException("Syntax error");
                                    }
                                    Next();
                                    break;
                                }
                                return Call(current.String, args);
                            }
                            if (variable.ContainsKey(current.String))
                                return variable[current.String];
                            else
                            {
                                var v = new Value(GetType(current.String));
                                variable.Add(current.String, v);
                                return v;
                            }
                        }
                    case TokenType.String:
                        return new Value(current.String);
                    case TokenType.Number:
                        return new Value(current.Number);
                    case TokenType.LParen:
                        {
                            var expr = Expr();
                            if (Current.Type != TokenType.RParen)
                                throw new BasicException("Syntax error");
                            Next();
                            return (expr);
                        }
                    case TokenType.MakeProcInstance:
                        {
                            var expr = Term();
                            return new Value(bf.MakeProcInstance(this.Interpreter, expr.String));
                        }
                    case TokenType.Minus:
                        {
                            var expr = Term();
                            return new Value(-expr.Number);
                        }
                    default:
                        throw new BasicException("unexcepp");
                }

            }

            private Value Call(string funcname, List<Value> args)
            {
                if (funcname.EndsWith("$"))
                {
                    funcname = funcname.TrimEnd('$');
                }
                var type = typeof(BuiltinFunction);
                Type[] types = args.Select(x => x.CLRType).ToArray();
                var info = type.GetMethod(funcname, types);
                return new Value(info.Invoke(bf, args.Select(x => x.Object).ToArray()));
            }

            public Value Expr2()
            {
                var expr = Term();
                while (true)
                {
                    var token = Current;
                    if (token.Type != TokenType.LBracket)
                    {
                        return expr;
                    }
                    Next();
                    var expr2 = Expr();
                    if (Current.Type == TokenType.RBracket)
                        Next();
                    else
                        throw new BasicException("Syntax error");
                    expr = expr[(int)expr2.Number];
                }
            }
            public Value Expr3()
            {
                var expr = Expr2();
                while (true)
                {
                    var token = Current;
                    if (token.Type != TokenType.Mul && token.Type != TokenType.Div && token.Type != TokenType.Mod)
                    {
                        return expr;
                    }
                    Next();
                    var expr2 = Expr2();
                    if (token.Type == TokenType.Mul)
                    {
                        expr = new Value(expr.Number * expr2.Number);
                    }
                    if (token.Type == TokenType.Div)
                    {
                        expr = new Value(expr.Number / expr2.Number);
                    }
                    if (token.Type == TokenType.Mod)
                    {
                        expr = new Value(expr.Number % expr2.Number);
                    }
                }
            }
            public Value Expr4()
            {
                var expr = Expr3();
                while (true)
                {
                    var token = Current;
                    if (token.Type != TokenType.Plus && token.Type != TokenType.Minus)
                        return expr;
                    Next();
                    var expr2 = Expr3();
                    if (token.Type == TokenType.Plus)
                    {
                        if (expr.Type == ValueType.String)
                            expr = new Value(expr.String + expr2.String);
                        else
                            expr = new Value(expr.Number + expr2.Number);
                    }
                    if (token.Type == TokenType.Minus)
                        expr = new Value(expr.Number - expr2.Number);
                }
            }
            public Value Expr6()
            {
                var expr = Expr4();
                while (true)
                {
                    var token = Current;
                    if (token.Type != TokenType.Less && token.Type != TokenType.LessEq && token.Type != TokenType.Greater && token.Type != TokenType.GreaterEq && token.Type != TokenType.Equal && token.Type != TokenType.NotEqual)
                    {
                        return expr;
                    }
                    Next();
                    var expr2 = Expr4();
                    if (expr.Type == ValueType.String)
                    {
                        if (token.Type == TokenType.Equal)
                        {
                            expr = new Value(expr.String == expr2.String);
                        }
                        if (token.Type == TokenType.NotEqual)
                        {
                            expr = new Value(expr.String != expr2.String);
                        }
                    }
                    else
                    {
                        if (token.Type == TokenType.Equal)
                        {
                            expr = new Value(expr.Number == expr2.Number);
                        }
                        if (token.Type == TokenType.NotEqual)
                        {
                            expr = new Value(expr.Number != expr2.Number);
                        }
                    }
                    if (token.Type == TokenType.Less)
                    {
                        expr = new Value(expr.Number < expr2.Number);
                    }
                    if (token.Type == TokenType.LessEq)
                    {
                        expr = new Value(expr.Number <= expr2.Number);
                    }
                    if (token.Type == TokenType.Greater)
                    {
                        expr = new Value(expr.Number > expr2.Number);
                    }
                    if (token.Type == TokenType.GreaterEq)
                    {
                        expr = new Value(expr.Number >= expr2.Number);
                    }

                }
            }
            public Value Expr()
            {
                return Expr6();
            }
            public void Print()
            {
                while (true)
                {
                    Next();
                    var c = Current;
                    if (c.Type == TokenType.Colon || c.Type == TokenType.NewLine)
                        break;
                    var expr = Expr();
                    result.Append(expr);
                    c = Current;
                    if (c.Type == TokenType.Comma)
                    {
                        result.Append("\t");
                        continue;
                    }
                    if (c.Type == TokenType.Semicolon)
                    {
                        continue;
                    }
                    result.Append("\n");
                    break;
                }
            }
            void For()
            {
                Next();
                var v = Current; Next();
                if (v.Type != TokenType.Iden)
                    throw new BasicException("Syntax error");
                var varname = v.String;
                var eq = Current; Next();
                if (eq.Type != TokenType.Equal)
                    throw new BasicException("Syntax error");
                var init = Expr();
                variable[varname] = new Value(init.Number);
                var to = Current; Next();
                if (to.Type != TokenType.To)
                    throw new BasicException("Syntax error");
                var end = Expr();
                double step = 1;
                if (Current.String == "STEP")
                {
                    Next();
                    step = Expr().Number;
                }
                int si = i;
                var oldc = Current;
                while (true)
                {
                    var c = Current;
                    if (c.Type != TokenType.Next)
                    {
                        Statement(); c = Current;
                    }
                    if (c.Type == TokenType.Next)
                    {
                        variable[varname] = new Value(variable[varname].Number + step);
                        if (variable[varname].Number > end.Number)
                        {
                            Next();
                            break;
                        }
                        i = si;
                        m_current = oldc;
                    }
                    if (i >= Program.Length)
                        break;
                }
            }
            Stack<int> stack = new Stack<int>();
            void Gosub()
            {
                Next();
                var label = Current;
                if (label.Type != TokenType.Label)
                    throw new BasicException("Syntax error");
                Next();
                stack.Push(i);
                i = labelTable[label.String];
                p = false;
            }
            void Goto()
            {
                Next();
                var label = Current;
                if (label.Type != TokenType.Label)
                    throw new BasicException("Syntax error");
                Next();
                i = labelTable[label.String];
                p = false;
            }
            void Return()
            {
                i = stack.Pop();
                p = false;
            }
            void If()
            {
                Next();
                var cond = Expr();
                var then = Current;
                if (then.Type != TokenType.Then)
                    throw new BasicException("Syntax error");
                Next();
                var e = Program.IndexOf('\n', i);
                if (cond.Number != 0)
                {
                }
                else
                {
                    i = e;
                    p = false;
                }
            }
            void Statement()
            {
                var c = Current;
                switch (c.Type)
                {
                    case TokenType.Print:
                        Print();
                        break;
                    case TokenType.Label:
                    case TokenType.Colon:
                    case TokenType.NewLine:
                        Next();
                        break;
                    case TokenType.For:
                        For();
                        break;
                    case TokenType.Gosub:
                        Gosub();
                        break;
                    case TokenType.Goto:
                        Goto();
                        break;
                    case TokenType.Return:
                        Return();
                        break;
                    case TokenType.If:
                        If();
                        break;
                    case TokenType.Iden:
                        Let();
                        break;
                    case TokenType.Let:
                        Next();
                        Let();
                        break;
                    default:
                        throw new BasicException($"unexpected {(c.String != null ? c.String : c.Type.ToString())}");
                }
            }

            private void Let()
            {
                var v = Current; Next();
                if (v.Type != TokenType.Iden)
                    throw new BasicException("Syntax error");
                var eq = Current; Next();
                if (eq.Type == TokenType.LBracket)
                {
                    var index = Expr();
                    if (Current.Type != TokenType.RBracket)
                        throw new BasicException("Syntax error");
                    Next();
                    eq = Current; Next();
                    if (eq.Type != TokenType.Equal)
                        throw new BasicException("Syntax error");
                    var expr = Expr();
                    if (GetType(v.String) != expr.Type)
                    {
                        throw new BasicException("Type mismatch");
                    }
                    var va = variable[v.String];
                    va[(int)index.Number] = expr;
                }
                else
                {
                    if (eq.Type != TokenType.Equal)
                        throw new BasicException("Syntax error");
                    var expr = Expr();
                    if (!Assignable(expr.Type, v.String))
                    {
                        throw new BasicException("Type mismatch");
                    }
                    variable[v.String] = expr;
                }
            }

            private bool Assignable(ValueType type, string name)
            {
                if (variable.ContainsKey(name))
                    return variable[name].Type == type;
                else
                {
                    if (type == ValueType.Number || type == ValueType.String)
                        return GetType(name) == type;
                    if (type == ValueType.NumberArray)
                        return GetType(name) == ValueType.Number;
                    if (type == ValueType.StringArray)
                        return GetType(name) == ValueType.String;
                    return false;
                }
            }

            private ValueType GetType(string name)
            {
                if (name.EndsWith("$"))
                {
                    return ValueType.String;
                }
                return ValueType.Number;
            }

            internal int i;
            internal Dictionary<string, int> labelTable;
            public string Run(bool newinstance = true)
            {
                lock (this)
                {
                    result = new StringBuilder();
                    if (newinstance)
                    {
                        i = 0;
                        bool isfirst = true;
                        while (true)
                        {
                            var c = Current;
                            if (c.Type == TokenType.NewLine || isfirst)
                            {
                                if (!isfirst)
                                    Next();
                                if (Current.Type == TokenType.Label)
                                {
                                    labelTable.Add(Current.String, i);
                                }
                            }
                            isfirst = false;
                            if (i >= Program.Length)
                                break;
                            Next();
                        }
                        p = false;
                        i = 0;
                    }
                    while (true)
                    {
                        Statement();
                        if (i >= Program.Length)
                            break;
                    }
                    return result.ToString();
                }
            }

            public string Run(string label)
            {
                lock (this)
                {
                    this.i = labelTable[label];
                    p = false;
                    return Run(false);
                }
            }
            public void SetVariable(string v, string msg)
            {
                if (!Assignable(ValueType.String, v))
                    throw new BasicException("Type mismatch");
                variable[v] = new Value(msg);
            }
        }
        public string Run()
        {
            var inte = new Interpreter2();
            inte.Interpreter = this;
            inte.Program = Program;
            inte.bf = bf;
            inte.variable = variable;
            inte.labelTable = labelTable;
            return inte.Run();
        }
        public Interpreter2 Run(string label)
        {
            var inte = new Interpreter2();
            inte.Interpreter = this;
            inte.Program = Program;
            inte.bf = bf;
            inte.variable = variable;
            inte.labelTable = labelTable;
            inte.i = labelTable[label];
            return inte;
        }

    }
    class BuiltinFunction
    {
        public double INT(double x)
        {
            return Math.Floor(x);
        }
        Random random = new Random();
        public double RND()
        {
            lock (this)
            {
                return random.NextDouble();
            }
        }
        public string CHR(double d)
        {
            return ((char)d).ToString();
        }
        public double ASC(string s)
        {
            return s[0];
        }
        public string MID(string s, double a, double b)
        {
            return s.Substring((int)a, (int)b);
        }
        public string STR(double d)
        {
            return d.ToString();
        }
        public double VAL(string s)
        {
            double r;
            if (double.TryParse(s, out r))
                return r;
            return 0;
        }
        public string CALL(string arg)
        {
            var p = CommandParser.ParseCommand(arg, false);
            return p.Command.Process(p.Text, p.Argument);
        }
        public string CALL(string command, string message)
        {
            var p = CommandRegister.CommandList[command];
            return p.Process(message, "");
        }
        public string CALL(string command, string message, string arg)
        {
            var p = CommandRegister.CommandList[command];
            return p.Process(message, arg);
        }
        public StringArray DIMSTR(double dim)
        {
            var a = new StringArray((int)dim);
            dim = (int)dim;
            for (; dim > 0; dim--)
            {
                a.Add("");
            }
            return a;
        }
        public NumberArray DIMNUM(double dim)
        {
            var a = new NumberArray((int)dim);
            dim = (int)dim;
            for (; dim > 0; dim--)
            {
                a.Add(0);
            }
            return a;
        }

        public string MakeProcInstance(Interpreter interpreter, string label)
        {
            var lc = new LabelCommand(interpreter, label);
            CommandRegister.RegistLocal(lc);
            return lc.Name;
        }
    }
    class LabelCommand : ICommand
    {
        Interpreter Interpreter;
        string Label;
        public LabelCommand(Interpreter interpreter, string label)
        {
            Interpreter = interpreter;
            Label = label;
            Name = "func" + string.Join("", Guid.NewGuid().ToByteArray().Select(x => x.ToString("X02")));
        }
        public string Name
        {
            get;
            protected set;
        }

        public string Process(string msg, string arg)
        {
            var a = Interpreter.Run(Label);
            a.SetVariable("A1$", msg);
            a.SetVariable("A2$", arg);
            return a.Run(false);
        }
    }


}