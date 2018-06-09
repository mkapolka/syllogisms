using System.Collections.Generic;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Syllogisms {
    public class StateMachine {
        private Dictionary<string, Relation> relations = new Dictionary<string, Relation>();
        private Dictionary<string, Action> actions = new Dictionary<string, Action>();
        private Dictionary<string, Parser.Line[]> actionPayloads = new Dictionary<string, Parser.Line[]>();
        private Dictionary<string, System.Action<string[]>> callbacks = new Dictionary<string, System.Action<string[]>>();
        private Dictionary<string, List<Exclusion>> exclusions = new Dictionary<string, List<Exclusion>>();

        public class Exclusion {
            public string relationKey;
            public Rule rule;
            public bool[] boundVars;
        }

        public void Claim(string claim) {
            Parser.Binding binding = Parser.GetBinding(claim);
            this.Claim(binding);
        }

        public void Claim(Parser.Binding binding) {
            Relation relation = this.GetRelation(binding.key);

            if (this.exclusions.ContainsKey(binding.key)) {
                foreach (Exclusion exclusion in this.exclusions[binding.key]) {
                    this.RemoveExclusions(exclusion, binding);
                }
            }

            Pair[] values = StateMachine.TokenToPairs(binding.tokens);
            relation.AddFact(values);
        }

        private void RemoveExclusions(Exclusion exclusion, Parser.Binding binding) {
            // Remove any claims that would conflict with the input claim (given by binding)
            // via the given exclusion.
            Relation relation = this.GetRelation(exclusion.relationKey);
            Pair[] vars = new Pair[binding.tokens.Length];
            for (int i = 0; i < vars.Length; i++) {
                if (exclusion.boundVars[i]) {
                    vars[i] = Pair.Value(binding.tokens[i].value);
                } else {
                    Pair.Fresh();
                }
            }
            List<string[]> varsToRemove = new List<string[]>();
            foreach (Stream stream in exclusion.rule.GetGoal(vars).Walk(new Stream())) {
                string[] v = new string[vars.Length];
                for (int i = 0; i < v.Length; i++) {
                    v[i] = stream.Walk(binding.tokens[i].value);
                }
                varsToRemove.Add(v);
            }

            foreach (string[] factToRemove in varsToRemove) {
                relation.RemoveFact(factToRemove);
            }
        }

        private Relation GetRelation(string key) {
            if (!this.relations.ContainsKey(key)) {
                this.relations[key] = new Relation();
            }
            return this.relations[key];
        }

        private Relation GetRelation(Parser.Binding binding) {
            return this.GetRelation(binding.key);
        }

        private Action GetAction(string key) {
            if (!this.actions.ContainsKey(key)) {
                this.actions[key] = new Action();
            }
            return this.actions[key];
        }

        private Action GetAction(Parser.Binding binding) {
            return this.GetAction(binding.key);
        }

        private void LoadLine(Parser.Line line) {
            if (line.type == Parser.LineType.Claim) {
                if (line.parent == null) {
                    this.Claim(line.binding);
                } else { 
                    Relation claimRelation = this.GetRelation(line.binding);
                    Pair[] vars = TokenToPairs(line.binding.tokens);
                    Rule rule = this.WalkRule(line, vars);
                    claimRelation.AddRule(rule);
                }
            } else if (line.type == Parser.LineType.Action) {
                Action action = this.GetAction(line.binding);
                Pair[] vars = TokenToPairs(line.binding.tokens);
                Rule rule = this.WalkRule(line, vars, false);
                string payloadID = System.Guid.NewGuid().ToString();
                action.AddPayload(rule, payloadID);
                this.actionPayloads[payloadID] = line.children.ToArray();
            } else if (line.type == Parser.LineType.Exclusion) {
                Exclusion exclusion = this.CreateExclusion(line);
                this.AddExclusion(exclusion, line.binding.key);
            }

            foreach (Parser.Line child in line.children) {
                this.LoadLine(child);
            }
        }

        private void AddExclusion(Exclusion exclusion, string key) {
            if (!this.exclusions.ContainsKey(key)) {
                this.exclusions[key] = new List<Exclusion>();
            }
            this.exclusions[key].Add(exclusion);
        }

        private Exclusion CreateExclusion(Parser.Line line) {
            Exclusion exclusion = new Exclusion();
            exclusion.relationKey = line.binding.key;
            bool[] boundVars = new bool[line.binding.tokens.Length];

            Pair[] vars = TokenToPairs(line.binding.tokens);
            Rule rule = this.WalkRule(line, vars, false);
            exclusion.rule = rule;

            Parser.Line parent = line.parent;
            while (parent != null) {
                foreach (Parser.Token token in parent.binding.tokens) {
                    for (int i = 0; i < boundVars.Length; i++) {
                        if (token.value == line.binding.tokens[i].value) {
                            boundVars[i] = true;
                        }
                    }
                }
                parent = parent.parent;
            }
            exclusion.boundVars = boundVars;
            return exclusion;
        }

        private Rule WalkRule(Parser.Line line, Pair[] vars, bool salt = true) {
            Rule rule = new Rule(vars, salt);
            
            Parser.Line parent = line.parent;
            while (parent != null) {
                Relation condRelation = this.GetRelation(parent.binding);
                Pair[] condVars = TokenToPairs(parent.binding.tokens);
                rule.AddCondition(condRelation, condVars);
                parent = parent.parent;
            }
            return rule;
        }

        public void PerformAction(string query) {
            Parser.Binding binding = Parser.GetBinding(query);
            this.PerformAction(binding);
        }

        public void PerformAction(Parser.Binding binding) {
            Action action = this.GetAction(binding);
            Pair[] vars = TokenToPairs(binding.tokens);
            Pair outKey = Pair.Fresh();
            foreach (Stream stream in action.GetGoal(vars, outKey).Walk(new Stream())) {
                string payloadKey = stream.Walk(outKey).key;
                Parser.Line[] payload = this.actionPayloads[payloadKey];
                foreach (Parser.Line line in payload) {
                    if (line.type == Parser.LineType.Callback) {
                        string name = Regex.Match(line.content, "^([a-zA-Z0-9]+):").Groups[1].Value;
                        string restOfLine = Regex.Replace(line.content, "^[a-zA-Z0-9]+: ?", "");
                        restOfLine = restOfLine.Trim();
                        if (name == "claim") {
                            Parser.Binding reified = this.GetBindingWithStream(restOfLine, stream);
                            this.Claim(reified);
                        } else if (name == "action") {
                            Parser.Binding reified = this.GetBindingWithStream(restOfLine, stream);
                            this.PerformAction(reified);
                        } else {
                            string[] variables = this.WalkTokens(line.binding.tokens, stream);
                            this.DoCallback(name, variables);
                        }
                    }
                }
                return;
            }
        }

        private Parser.Binding GetBindingWithStream(string line, Stream stream) {
            Parser.Binding initial = Parser.GetBinding(line);
            Parser.Token[] tokens = new Parser.Token[initial.tokens.Length];
            string[] walked = this.WalkTokens(initial.tokens, stream);
            for (int i = 0; i < tokens.Length; i++) {
                tokens[i] = new Parser.Token();
                tokens[i].type = walked[i] == null ? Parser.TokenType.Variable : Parser.TokenType.String;
                tokens[i].value = walked[i];
            }
            initial.tokens = tokens;
            return initial;
        }

        private string[] WalkTokens(Parser.Token[] tokens, Stream stream) {
            string[] output = new string[tokens.Length];
            for (int i = 0; i < tokens.Length; i++) {
                Parser.Token token = tokens[i];
                string tokenValue = token.value;
                if (token.type == Parser.TokenType.Variable) {
                    tokenValue = stream.Walk(token.value);
                }
                output[i] = tokenValue;
            }
            return output;
        }

        public void AddCallback(string name, System.Action<string[]> callback) {
            this.callbacks[name] = callback;
        }

        private void DoCallback(string name, string[] values) {
            this.callbacks[name](values);
        }

        public void ReadFile(string file) {
            Parser.Line[] lines = Parser.ParseFile(file);
            foreach (Parser.Line line in lines) {
                this.LoadLine(line);
            }
        }

        private static Pair[] TokenToPairs(Parser.Token[] tokens) {
            Pair[] output = new Pair[tokens.Length];
            for (int i = 0; i < output.Length; i++) {
                Parser.Token token = tokens[i];
                if (token.type == Parser.TokenType.Variable) {
                    output[i] = Pair.Variable(token.value);
                } else {
                    output[i] = Pair.Value(token.value);
                }
            }
            return output;
        }

        private IEnumerable<Stream> Query(string query) {
            Parser.Binding binding = Parser.GetBinding(query);
            Relation relation = this.relations[binding.key];
            Pair[] vars = TokenToPairs(binding.tokens);
            Stream stream = new Stream();
            foreach (Stream walkStream in relation.Query(vars).Walk(stream)) {
                yield return walkStream;
            }
        }

        public bool IsTrue(string query) {
            foreach (Stream walkStream in this.Query(query)) {
                return true;
            }
            return false;
        }
        
        public string[] GetValues(string query) {
            Parser.Binding binding = Parser.GetBinding(query);
            foreach (Stream stream in this.Query(query)) {
                return this.WalkTokens(binding.tokens, stream);
            }
            return null;
        }
    }
}
