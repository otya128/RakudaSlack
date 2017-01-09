using System;
using RakudaSlack;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StringArray = System.Collections.Generic.List<string>;
using NumberArray = System.Collections.Generic.List<double>;
using System.Collections;

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
        Def,
        End,
        And,
        Or,
        Xor,
        Class,
        Dim,
        New,
        Dot,
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
        Instance,
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
                    case ValueType.NumberArray:
                        return typeof(NumberArray);
                    case ValueType.StringArray:
                        return typeof(StringArray);
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

        public Instance Instance
        {
            get
            {
                if (Type != ValueType.Instance)
                    throw new BasicException("Type mismatch");
                return mobject as Instance;
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
            if (obj == null)
            {
                Type = ValueType.None;
                return;
            }
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
        public Value(Instance obj) : this()
        {
            mobject = obj;
            Type = ValueType.Instance;
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
                case ValueType.Instance:
                    return (mobject as Instance).Class.Name;
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
        Dictionary<string, Function> FunctionTable = new Dictionary<string, Function>();
        IDictionary<string, Class> ClassTable = new Dictionary<string, Class>();

        public Interpreter(string msg)
        {
            this.Program = msg;
        }
        public class Interpreter2
        {
            internal string Program;
            internal BuiltinFunction bf;
            internal IDictionary<string, Value> variable;
            internal IDictionary<string, Class> ClassTable;
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
                        case '.': return new Token(TokenType.Dot);
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
                            case "DEF":
                                return new Token(TokenType.Def, v);
                            case "END":
                                return new Token(TokenType.End, v);
                            case "AND":
                                return new Token(TokenType.And, v);
                            case "OR":
                                return new Token(TokenType.Or, v);
                            case "XOR":
                                return new Token(TokenType.Xor, v);
                            case "CLASS":
                                return new Token(TokenType.Class, v);
                            case "DIM":
                                return new Token(TokenType.Dim, v);
                            case "NEW": return new Token(TokenType.New, v);
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
            List<Value> Arguments()
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
                return args;
            }
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
                                return Call(current.String, Arguments());
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
                    case TokenType.New:
                        {
                            if (Current.Type != TokenType.Iden)
                                throw new BasicException("Syntax error(NEW)");
                            Class @class;
                            if (!ClassTable.TryGetValue(Current.String, out @class))
                                throw new BasicException("Undefined class");
                            Next();
                            return new Value(@class.New());
                        }
                    case TokenType.LBracket:
                        {
                            NumberArray na = null;
                            StringArray sa = null;
                            while (Current.Type != TokenType.RBracket)
                            {
                                var expr = Expr();
                                if (na == null && sa == null)
                                {
                                    if (expr.Type == ValueType.Number)
                                        na = new NumberArray();
                                    if (expr.Type == ValueType.String)
                                        sa = new StringArray();
                                }
                                if (na != null)
                                    na.Add(expr.Number);
                                if (sa != null)
                                    sa.Add(expr.String);
                                if (Current.Type == TokenType.RBracket)
                                    break;
                                if (Current.Type != TokenType.Comma)
                                    throw new BasicException("Syntax error");
                                Next();
                            }
                            Next();
                            if (na != null)
                                return new Value(na);
                            if (sa != null)
                                return new Value(sa);
                            throw new BasicException("Not impl");
                        }
                    default:
                        throw new BasicException("unexcepp");
                }

            }
            
            private Value Call(Function func, List<Value> args)
            {
                var inte = new Interpreter2(Interpreter);
                inte.result = this.result;
                return inte.CallUserFunction(func, args);
            }


            private Value Call(string funcname, List<Value> args)
            {
                if (FunctionTable.ContainsKey(funcname))
                {
                    return Call(FunctionTable[funcname], args);
                }
                if (funcname.EndsWith("$"))
                {
                    funcname = funcname.TrimEnd('$');
                }
                var type = typeof(BuiltinFunction);
                Type[] types = args.Select(x => x.CLRType).ToArray();
                var info = type.GetMethod(funcname, types);
                return new Value(info.Invoke(bf, args.Select(x => x.Object).ToArray()));
            }

            public Value Expr1()
            {
                var expr = Term();
                while (true)
                {
                    var token = Current;
                    if (token.Type != TokenType.Dot)
                    {
                        return expr;
                    }
                    Next();
                    if (Current.Type != TokenType.Iden)
                        throw new BasicException("Syntax error(.)");
                    var expr2 = Current.String;
                    Next();
                    if (expr.Type != ValueType.Instance)
                        throw new BasicException("Type mismatch(.)");
                    if (Current.Type == TokenType.LParen)
                    {
                        var args = Arguments();
                        var func = expr.Instance.GetFunction(expr2);
                        expr = Call(new InstanceFunction(func, expr.Instance), args);
                    }
                    else
                        expr = expr.Instance.Variable[expr2];
                }
            }
            public Value Expr2()
            {
                var expr = Expr1();
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
            public Value Expr7()
            {
                var expr = Expr6();
                while (true)
                {
                    var token = Current;
                    if (token.Type != TokenType.And)
                    {
                        return expr;
                    }
                    Next();
                    var expr2 = Expr6();
                    if (token.Type == TokenType.And)
                    {
                        expr = new Value((int)expr.Number & (int)expr2.Number);
                    }
                }
            }
            public Value Expr9()
            {
                var expr = Expr7();
                while (true)
                {
                    var token = Current;
                    if (token.Type != TokenType.Or && token.Type != TokenType.Xor)
                    {
                        return expr;
                    }
                    Next();
                    var expr2 = Expr7();
                    if (token.Type == TokenType.Or)
                    {
                        expr = new Value((int)expr.Number | (int)expr2.Number);
                    }
                    if (token.Type == TokenType.Xor)
                    {
                        expr = new Value((int)expr.Number ^ (int)expr2.Number);
                    }
                }
            }
            public Value Expr()
            {
                return Expr9();
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
                if (variable[varname].Number > end.Number)
                {
                    FindForNext();
                    return;
                }
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

            private void FindForNext()
            {
                int forcount = 0;
                while (forcount >= 0 && i < Program.Length)
                {
                    var c = Current;
                    switch (c.Type)
                    {
                        case TokenType.Next:
                            forcount--;
                            break;
                        case TokenType.For:
                            forcount++;
                            break;
                        case TokenType.If:
                            i = Program.IndexOf('\n', i);
                            if (i == -1)
                                i = Program.Length;
                            p = false;
                            break;
                        case TokenType.Def:
                            Next();
                            i = FunctionTable[Current.String].End;
                            p = false;
                            break;
                        default:
                            break;
                    }
                    Next();
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
                if (IsFunction)
                {
                    Next();
                    ReturnValue = Expr();
                    i = Program.Length;
                    return;
                }
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
                if (e == -1)
                    e = Program.Length;
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
                    case TokenType.Def:
                        Next();
                        i = FunctionTable[Current.String].End;
                        p = false;
                        break;
                    case TokenType.End:
                        i = Program.Length;
                        break;
                    case TokenType.Class:
                        Next();
                        i = ClassTable[Current.String].End;
                        p = false;
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
                var eq = Current;
                if (eq.Type == TokenType.LBracket)
                {
                    Next();
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
                    if (eq.Type == TokenType.Dot)
                    {
                        var e = variable[v.String];
                        var idenname = v.String;
                        while (true)
                        {
                            Next();
                            if (Current.Type != TokenType.Iden)
                            {
                                throw new BasicException("Syntax error");
                            }
                            idenname = Current.String;
                            Next();
                            if (Current.Type == TokenType.Equal)
                            {
                                Next();
                                e.Instance.Variable[idenname] = Expr();
                                return;
                            }
                            if (Current.Type != TokenType.Dot)
                            {
                                break;
                            }
                            e = e.Instance.Variable[idenname];
                        }
                        List<Value> args = new List<Value>();
                        while (IsExpr(Current.Type))
                        {
                            args.Add(Expr());
                            if (Current.Type != TokenType.Comma)
                            {
                                break;
                            }
                            Next();
                        }
                        Call(new InstanceFunction(e.Instance.GetFunction(idenname), e.Instance), args);
                        return;
                    }
                    if (eq.Type != TokenType.Equal)
                    {
                        FuncStatement(v.String);
                        return;
                    }
                    Next();

                    var expr = Expr();
                    if (!Assignable(expr.Type, v.String))
                    {
                        throw new BasicException("Type mismatch");
                    }
                    variable[v.String] = expr;
                }
            }

            private void FuncStatement(string funcname)
            {
                List<Value> args = new List<Value>();
                while (IsExpr(Current.Type))
                {
                    args.Add(Expr());
                    if (Current.Type == TokenType.Comma)
                    {
                        Next();
                        continue;
                    }
                    Next();
                    break;
                }
                Call(funcname, args);
            }

            private bool IsExpr(TokenType type)
            {
                switch (type)
                {
                    case TokenType.Iden:
                    case TokenType.New:
                    case TokenType.Number:
                    case TokenType.Minus:
                    case TokenType.MakeProcInstance:
                    case TokenType.String:
                        return true;
                }
                return false;
            }

            private bool Assignable(ValueType type, string name)
            {
                if (variable.ContainsKey(name))
                {
                    var v = variable[name];
                    if (v.Type == ValueType.None)
                        return true;
                    return v.Type == type;
                }
                else
                {
                    if (type == ValueType.Number || type == ValueType.String)
                        return GetType(name) == type;
                    if (type == ValueType.NumberArray || type == ValueType.Instance)
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
            internal Dictionary<string, Function> FunctionTable;

            public Interpreter2(Interpreter interpreter)
            {
                Interpreter = interpreter;
                Program = interpreter.Program;
                bf = interpreter.bf;
                variable = interpreter.variable;
                labelTable = interpreter.labelTable;
                FunctionTable = interpreter.FunctionTable;
                ClassTable = interpreter.ClassTable;
            }

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
                            if (Current.Type == TokenType.Def)
                            {
                                var f = Defun();
                                FunctionTable.Add(f.Name, f);
                            }
                            if (Current.Type == TokenType.Class)
                            {
                                DefClass();
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

            private void DefClass()
            {
                Next();
                if (Current.Type != TokenType.Iden)
                {
                    throw new BasicException("Syntax error(CLASS)");
                }
                var name = Current.String;
                Next();
                List<string> field = new List<string>();
                Dictionary<string, Function> func = new Dictionary<string, Function>();
                while (i < Program.Length)
                {
                    var c = Current;
                    if (c.Type == TokenType.NewLine || c.Type == TokenType.Colon)
                    {
                        Next();
                        continue;
                    }
                    if (c.Type == TokenType.Dim)
                    {
                        Next();
                        if (Current.Type != TokenType.Iden)
                        {
                            throw new BasicException("Syntax error(CLASS(DIM))");
                        }
                        field.Add(Current.String);
                        Next();
                        continue;
                    }
                    if (c.Type == TokenType.Def)
                    {
                        var f = Defun();
                        func.Add(f.Name, f);
                        continue;
                    }
                    if (c.Type == TokenType.End)
                    {
                        break;
                    }
                    throw new BasicException("Syntax error(CLASS)");
                }
                Next();
                var @class = new Class { End = i, FunctionTable = func, Name = name, Parent = null, Variable = field };
                ClassTable.Add(name, @class);
            }

            bool IsFunction;
            Value ReturnValue;

            public Value CallUserFunction(Function func, List<Value> value)
            {
                if (func.In.Length != value.Count)
                    throw new BasicException("Illegal function call");
                var local = func.Init(value);
                labelTable = func.LabelTable;
                variable = new DoubleDictionary<string, Value>(variable, local);
                i = func.Start;
                IsFunction = true;

                while (true)
                {
                    Statement();
                    if (i >= Program.Length)
                        break;
                }
                return ReturnValue;
            }
            //DEF IDEN'('(IDEN',')*IDEN?')'
            private Function Defun()
            {
                Next();
                if (Current.Type != TokenType.Iden)
                    throw new BasicException("Syntax error(DEF)");
                var name = Current.String;
                Next();
                bool hasValue;
                if (Current.Type == TokenType.LParen)
                {
                    Next();
                    hasValue = true;
                }
                else
                    hasValue = false;
                List<string> inargs = new List<string>();
                while (Current.Type == TokenType.Iden)
                {
                    inargs.Add(Current.String);
                    Next();
                    if (Current.Type != TokenType.Comma)
                        break;
                    Next();
                }
                if (hasValue)
                {
                    if (Current.Type != TokenType.RParen)
                    {
                        throw new BasicException("Syntax error(DEF)");
                    }
                    Next();
                }
                var si = i;
                var lt = new Dictionary<string, int>();
                bool isfirst = false;
                while (Current.Type != TokenType.End && i < Program.Length)
                {
                    var c = Current;
                    if (c.Type == TokenType.NewLine || isfirst)
                    {
                        if (!isfirst)
                            Next();
                        if (Current.Type == TokenType.Label)
                        {
                            lt.Add(Current.String, i);
                        }
                    }
                    if (Current.Type == TokenType.End)
                        break;
                    Next();
                }
                Next();
                var func = new Function { End = i, Start = si, HasReturnValue = hasValue, In = inargs.ToArray(), LabelTable = lt, Name = name };
                return func;
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
            var inte = new Interpreter2(this);
            return inte.Run();
        }
        public Interpreter2 Run(string label)
        {
            var inte = new Interpreter2(this);
            inte.i = labelTable[label];
            return inte;
        }

    }

    internal class DoubleDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        IDictionary<TKey, TValue> BaseDictionary;
        IDictionary<TKey, TValue> BaseDictionary2;
        public DoubleDictionary(IDictionary<TKey, TValue> dictionary, IDictionary<TKey, TValue> dictionary2)
        {
            BaseDictionary = dictionary;
            BaseDictionary2 = dictionary2;
        }
        public TValue this[TKey key]
        {
            get
            {
                if (BaseDictionary2.ContainsKey(key))
                    return BaseDictionary2[key];
                return BaseDictionary[key];
            }
            set
            {
                if (BaseDictionary2.ContainsKey(key))
                {
                    BaseDictionary2[key] = value;
                }
                if (BaseDictionary.ContainsKey(key))
                {
                    BaseDictionary[key] = value;
                }
                BaseDictionary2[key] = value;
            }
        }

        public int Count
        {
            get
            {
                return this.BaseDictionary.Count + BaseDictionary2.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return this.BaseDictionary.IsReadOnly && this.BaseDictionary2.IsReadOnly;
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            this.BaseDictionary2.Add(item);
        }

        public void Add(TKey key, TValue value)
        {
            this.BaseDictionary2.Add(key, value);
        }

        public void Clear()
        {
            this.BaseDictionary.Clear();
            this.BaseDictionary2.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return this.BaseDictionary2.Contains(item) || this.BaseDictionary.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return BaseDictionary.ContainsKey(key) || BaseDictionary2.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var item in BaseDictionary)
            {
                yield return item;
            }
            foreach (var item in BaseDictionary2)
            {
                yield return item;
            }
        }

        public bool Remove(TKey key)
        {
            return BaseDictionary2.Remove(key) || BaseDictionary.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return BaseDictionary2.Remove(item) || BaseDictionary.Remove(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return BaseDictionary2.TryGetValue(key, out value) || BaseDictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var item in BaseDictionary)
            {
                yield return item;
            }
            foreach (var item in BaseDictionary2)
            {
                yield return item;
            }
        }
    }

    class Function
    {
        public string[] In;
        public bool HasReturnValue;
        public int Start, End;
        public Dictionary<string, int> LabelTable;
        public string Name;

        internal virtual IDictionary<string, Value> Init(List<Value> args)
        {
            var local = new Dictionary<string, Value>();
            int j = 0;
            foreach (var item in In)
            {
                local.Add(item, args[j++]);//TODO:
            }
            return local;
        }
    }
    class InstanceFunction : Function
    {
        public InstanceFunction(Function @base, Instance ins)
        {
            Instance = ins;
            In = @base.In;
            Name = @base.Name;
            LabelTable = @base.LabelTable;
            HasReturnValue = @base.HasReturnValue;
            Start = @base.Start;
            End = @base.End;
        }
        Instance Instance;
        internal override IDictionary<string, Value> Init(List<Value> args)
        {
            return new DoubleDictionary<string, Value>(Instance.Variable, base.Init(args));
        }
    }
    class Class
    {
        public string Name;
        public Class Parent;
        public IDictionary<string, Function> FunctionTable;
        public List<string> Variable;
        public int End;
        public Instance New()
        {
            var inst = new Instance { Class = this, Variable = new Dictionary<string, Value>() };
            foreach (var item in Variable)
            {
                inst.Variable.Add(item, new Value());
            }
            return inst;
        }
    }
    class Instance
    {
        public Class Class;
        public IDictionary<string, Value> Variable;

        public Function GetFunction(string expr2)
        {
            return Class.FunctionTable[expr2];
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
        public void WAIT(double time)
        {
            System.Threading.Thread.Sleep((int)time);
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
        public double LEN(string a)
        {
            return a.Length;
        }
        public double LEN(NumberArray a)
        {
            return a.Count;
        }
        public double LEN(StringArray a)
        {
            return a.Count;
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
            try
            {
                if (Label.StartsWith("@"))
                {
                    var a = Interpreter.Run(Label);
                    a.SetVariable("A1$", msg);
                    a.SetVariable("A2$", arg);
                    return a.Run(false);
                }
                else
                {
                    var a = new Interpreter.Interpreter2(Interpreter);
                    return a.CallUserFunction(a.FunctionTable[Label], new List<Value> { new Value(msg), new Value(arg) }).ToString();
                }
            }
            catch (BasicException e)
            {
                return e.Message;
            }
        }
    }
}