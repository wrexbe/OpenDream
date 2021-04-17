﻿using OpenDreamShared.Compiler;
using System;
using System.Collections.Generic;
using System.Text;

namespace DMCompiler.Preprocessor {
    class DMPreprocessorLexer : Lexer {
        public DMPreprocessorLexer(string source) : base(source) { }

        public Token GetNextTokenIgnoringWhitespace() {
            Token nextToken = GetNextToken();
            while (nextToken.Type == TokenType.DM_Preproc_Whitespace) nextToken = GetNextToken();

            return nextToken;
        }

        protected override Token ParseNextToken() {
            Token token = base.ParseNextToken();

            if (token.Type == TokenType.Unknown) {
                char c = GetCurrent();

                switch (c) {
                    case ' ':
                    case '\t': Advance(); token = CreateToken(TokenType.DM_Preproc_Whitespace, c); break;
                    case '}':
                    case '!':
                    case '&':
                    case '|':
                    case '%':
                    case '>':
                    case '<':
                    case '^':
                    case ':':
                    case ';':
                    case '?':
                    case '+':
                    case '-':
                    case '*':
                    case '~':
                    case '=': Advance(); token = CreateToken(TokenType.DM_Preproc_Punctuator, c); break;
                    case ',': Advance(); token = CreateToken(TokenType.DM_Preproc_Punctuator_Comma, c); break;
                    case '(': Advance(); token = CreateToken(TokenType.DM_Preproc_Punctuator_LeftParenthesis, c); break;
                    case ')': Advance(); token = CreateToken(TokenType.DM_Preproc_Punctuator_RightParenthesis, c); break;
                    case '[': Advance(); token = CreateToken(TokenType.DM_Preproc_Punctuator_LeftBracket, c); break;
                    case ']': Advance(); token = CreateToken(TokenType.DM_Preproc_Punctuator_RightBracket, c); break;
                    case '/': {
                        if (Advance() == '/') {
                            while (Advance() != '\n' && !IsAtEndOfFile()) ;

                            token = CreateToken(TokenType.Skip, "//");
                        } else if (GetCurrent() == '*') {
                            //Skip everything up to the "*/"
                            Advance();
                            while (true) {
                                bool isStar = GetCurrent() == '*';

                                if (isStar && Advance() == '/') break;
                                else if (IsAtEndOfFile()) throw new Exception("Expected \"*/\" to end multiline comment");
                                else if (!isStar) Advance();
                            }

                            Advance();
                            token = CreateToken(TokenType.Skip, "/* */");
                        } else {
                            token = CreateToken(TokenType.DM_Preproc_Punctuator, c);
                        }

                        break;
                    }
                    case '@': { //Raw string
                        char delimiter = Advance();
                        StringBuilder textBuilder = new StringBuilder();

                        textBuilder.Append('@');
                        textBuilder.Append(delimiter);
                        while ((c = Advance()) != delimiter) {
                            textBuilder.Append(c);
                        }
                        Advance();

                        string text = textBuilder.ToString();
                        token = CreateToken(TokenType.DM_Preproc_ConstantString, text, text.Substring(2, text.Length - 3));
                        break;
                    }
                    case '\'':
                    case '"': {
                        token = LexString(false);

                        break;
                    }
                    case '{': {
                        if (Advance() == '"') {
                            token = LexString(true);
                        } else {
                            token = CreateToken(TokenType.DM_Preproc_Punctuator, c);
                        }

                        break;
                    }
                    case '#': {
                        StringBuilder textBuilder = new StringBuilder(Convert.ToString(c));
                        while ((IsAlphabetic(Advance()) ||GetCurrent() == '_' || GetCurrent() == '#') && !IsAtEndOfFile()) {
                            textBuilder.Append(GetCurrent());
                        }

                        string text = textBuilder.ToString();
                        if (text == "#include") {
                            token = CreateToken(TokenType.DM_Preproc_Include, text);
                        } else if (text == "#define") {
                            token = CreateToken(TokenType.DM_Preproc_Define, text);
                        } else if (text == "#undef") {
                            token = CreateToken(TokenType.DM_Preproc_Undefine, text);
                        } else if (text == "#if") {
                            token = CreateToken(TokenType.DM_Preproc_If, text);
                        } else if (text == "#ifdef") {
                            token = CreateToken(TokenType.DM_Preproc_Ifdef, text);
                        } else if (text == "#ifndef") {
                            token = CreateToken(TokenType.DM_Preproc_Ifndef, text);
                        } else if (text == "#else") {
                            token = CreateToken(TokenType.DM_Preproc_Else, text);
                        } else if (text == "#endif") {
                            token = CreateToken(TokenType.DM_Preproc_EndIf, text);
                        } else if (text.StartsWith("##")) {
                            token = CreateToken(TokenType.DM_Preproc_TokenConcat, text, text.Substring(2));
                        } else {
                            token = CreateToken(TokenType.DM_Preproc_ParameterStringify, text, text.Substring(1));
                        }

                        break;
                    }
                    default: {
                        if (IsAlphabetic(c) || c == '_') {
                            StringBuilder textBuilder = new StringBuilder(Convert.ToString(c));
                            while ((IsAlphanumeric(Advance()) || GetCurrent() == '_') && !IsAtEndOfFile()) textBuilder.Append(GetCurrent());

                            token = CreateToken(TokenType.DM_Preproc_Identifier, textBuilder.ToString());
                        } else if (IsNumeric(c) || c == '.') {
                            StringBuilder textBuilder = new StringBuilder(Convert.ToString(c));

                            if (c == '.') {
                                c = Advance();

                                if (!IsNumeric(c)) token = CreateToken(TokenType.DM_Preproc_Punctuator_Period, '.');
                                else textBuilder.Append(c);
                            }

                            if (IsNumeric(c) || c == '#') {
                                while (!IsAtEndOfFile()) {
                                    c = Advance();

                                    if (IsNumeric(c) || c == 'e' || c == 'E' || c == 'p' || c == 'P') {
                                        textBuilder.Append(c);
                                    } else {
                                        break;
                                    }
                                }

                                token = CreateToken(TokenType.DM_Preproc_Number, textBuilder.ToString());
                            }
                        } else {
                            Advance();
                        }

                        break;
                    }
                }
            }

            return token;
        }

        //Lexes a string
        //If it contains string interpolations, it splits the string tokens into parts and lexes the expressions as normal
        //For example, "There are [amount] of them" becomes:
        //    DM_Preproc_String("There are "), DM_Preproc_Identifier(amount), DM_Preproc_String( of them")
        //If there is no string interpolation, it outputs a DM_Preproc_ConstantString token instead
        private Token LexString(bool isLong) {
            char terminator = GetCurrent();
            StringBuilder textBuilder = new StringBuilder(isLong ? "{" + terminator : Convert.ToString(terminator));
            Queue<Token> stringTokens = new();

            Advance();
            while (!(!isLong && GetCurrent() == '\n') && !IsAtEndOfFile()) {
                char stringC = GetCurrent();

                textBuilder.Append(stringC);
                if (stringC == '[') {
                    stringTokens.Enqueue(CreateToken(TokenType.DM_Preproc_String, textBuilder.ToString()));
                    textBuilder.Clear();

                    Advance();

                    Token exprToken = GetNextToken();
                    int bracketNesting = 0;
                    while (!(bracketNesting == 0 && exprToken.Type == TokenType.DM_Preproc_Punctuator_RightBracket) && !IsAtEndOfFile()) {
                        stringTokens.Enqueue(exprToken);

                        if (exprToken.Type == TokenType.DM_Preproc_Punctuator_LeftBracket) bracketNesting++;
                        if (exprToken.Type == TokenType.DM_Preproc_Punctuator_RightBracket) bracketNesting--;
                        exprToken = GetNextToken();
                    }

                    if (exprToken.Type != TokenType.DM_Preproc_Punctuator_RightBracket) throw new Exception("Expected ']' to end expression");
                    textBuilder.Append(']');
                } else if (stringC == '\\') {
                    Advance();
                    textBuilder.Append(GetCurrent());
                    Advance();
                } else if (stringC == terminator) {
                    if (isLong) {
                        stringC = Advance();

                        if (stringC == '}') {
                            textBuilder.Append('}');

                            break;
                        }
                    } else {
                        break;
                    }
                } else {
                    Advance();
                }
            }

            Advance();

            string text = textBuilder.ToString();
            if (!isLong && !text.EndsWith(terminator)) throw new Exception("Expected '" + terminator + "' to end string");
            else if (isLong && !text.EndsWith("}")) throw new Exception("Expected '}' to end long string");

            if (stringTokens.Count == 0) {
                return CreateToken(TokenType.DM_Preproc_ConstantString, text, text.Substring(1, text.Length - 2));
            } else {
                stringTokens.Enqueue(CreateToken(TokenType.DM_Preproc_String, textBuilder.ToString()));

                foreach (Token stringToken in stringTokens) {
                    _pendingTokenQueue.Enqueue(stringToken);
                }

                return CreateToken(TokenType.Skip, null);
            }
        }
    }
}