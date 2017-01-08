using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RakudaSlack
{
    enum TokenType
    {
        LParen,
        RParen,
        Iden,
        String,
        Pipe,
        Comma,
        End,
        Dot,
    }
    class Token
    {
        public string Value
        {
            get;
            protected set;
        }
        public TokenType Type
        {
            get;
            protected set;
        }
        public Token(TokenType type)
        {
            Type = type;
        }
        public Token(TokenType type, string value) : this(type)
        {
            Value = value;
        }
    }
}
