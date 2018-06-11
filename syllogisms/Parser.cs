using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Syllogisms {
    public class Parser {
        public enum TokenType {
            String, Variable
        }

        public enum LineType {
            Condition, Claim, Action, Callback, Exclusion, String, Comment
        }

        public struct Binding {
            public string key;
            public Token[] tokens;
        }

        public struct Token {
            public TokenType type;
            public string value;
        }

        public class Line {
            public LineType type;
            public string text;
            public Binding binding;
            public Line parent;
            public List<Line> children = new List<Line>();
            public int indentLevel;
            public string content;
        }

        public static int GetIndentLevel(string line) {
            return Regex.Match(line, "^ *").Length;
        }

        public static Line ParseLine(string line) {
            Line output = new Line();
            output.text = line;
            output.indentLevel = Parser.GetIndentLevel(line);
            line = Regex.Replace(line, "^ *", "");
            if (line.StartsWith("::")) {
                output.type = LineType.Condition;
                line = Regex.Replace(line, "^:: ?", "");
            } else if (line.StartsWith("+")) {
                output.type = LineType.Claim;
                line = Regex.Replace(line, "^\\+ ?", "");
            } else if (line.StartsWith("*")) {
                output.type = LineType.Action;
                line = Regex.Replace(line, "^\\* ?", "");
            } else if (line.StartsWith(">")) {
                output.type = LineType.Callback;
                line = Regex.Replace(line, "^> ?", "");
            } else if (line.StartsWith("?")) {
                output.type = LineType.Exclusion;
                line = Regex.Replace(line, "^\\? ?", "");
            } else if (line.StartsWith("//")) {
                output.type = LineType.Comment;
                line = Regex.Replace(line, "^// ?", "");
            } else {
                output.type = LineType.String;
            }
            output.binding = Parser.GetBinding(line);
            output.content = line;
            return output;
        }

        public static Line[] ParseFile(string file) {
            Stack<Line> lineStack = new Stack<Line>();
            List<Line> lines = new List<Line>();
            foreach (string lineString in file.Split('\n')) {
                if (lineString.Trim().Length == 0) {
                    continue;
                }

                Line line = Parser.ParseLine(lineString);

                Line parent = lineStack.Count > 0 ? lineStack.Peek() : null;
                while (parent != null && line.indentLevel <= parent.indentLevel) {
                    if (lineStack.Count > 0) {
                        parent = lineStack.Pop();
                    } else {
                        parent = null;
                    }
                }

                lineStack.Push(line);

                line.parent = parent;
                if (parent != null) {
                    parent.children.Add(line);
                } else {
                    lines.Add(line);
                }
            }
            return lines.ToArray();
        }

        public static Token ParseToken(string input) {
            Token output = new Token();

            if (input[0] == '"') {
                output.type = TokenType.String;
                output.value = input.Trim(new char[]{'"'});
            } else if (input[0] == '(') {
                output.type = TokenType.Variable;
                output.value = input.Trim(new char[]{'(', ')'});
            } else {
                throw new Exception("Invalid token");
            }

            return output;
        }

        public static Binding GetBinding(string query) {
            string variablePattern = "\\([^\\)]*\\)";
            string stringPattern = "\"[^\"]*\"";
            string tokenPattern = variablePattern + "|" + stringPattern;
            Binding output = new Binding();
            output.key = Regex.Replace(
                query,
                tokenPattern,
                "%s");

            output.key = Regex.Replace(
                output.key,
                stringPattern,
                "%s");

            MatchCollection matches = Regex.Matches(query, tokenPattern);
            output.tokens = new Token[matches.Count];

            for (int i = 0; i < matches.Count; i++) {
                Match match = matches[i];
                output.tokens[i] = ParseToken(match.Value);
            }
            return output;
        }
    }
}
