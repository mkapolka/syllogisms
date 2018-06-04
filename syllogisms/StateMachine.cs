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
        private Dictionary<string, Exclusion> exclusions = new Dictionary<string, Exclusion>();

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
            Debug.Log("Claiming " + binding.key);
            Relation relation = this.GetRelation(binding.key);

            if (this.exclusions.ContainsKey(binding.key)) {
                Debug.Log("Removing exclusion " + binding.key);
                Exclusion exclusion = this.exclusions[binding.key];
                this.RemoveExclusions(exclusion, binding);
            }

            string[] values = StateMachine.TokenToVariables(binding.tokens);
            relation.AddFact(values);
        }

        public void RemoveExclusions(Exclusion exclusion, Parser.Binding binding) {
            Relation relation = this.GetRelation(exclusion.relationKey);
            string[] vars = new string[binding.tokens.Length];
            for (int i = 0; i < vars.Length; i++) {
                if (exclusion.boundVars[i]) {
                    vars[i] = binding.tokens[i].value;
                } else {
                    vars[i] = "._" + i;
                }
                Debug.Log("Vars: " + i + ":" + vars[i]);
            }
            Goal toRemove = new Conj(
                exclusion.rule.GetGoal(vars),
                relation.GetGoal(vars)
            );
            List<string[]> varsToRemove = new List<string[]>();
            foreach (Stream stream in exclusion.rule.GetGoal(vars).Walk(new Stream())) {
                Debug.Log("Streamy match!!");
                string[] v = new string[vars.Length];
                for (int i = 0; i < v.Length; i++) {
                    v[i] = stream.Walk(binding.tokens[i].value);
                    Debug.Log("v" + i + ": " + v[i]);
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
                    string[] vars = TokenToVariables(line.binding.tokens);
                    Rule rule = this.WalkRule(line, vars);
                    claimRelation.AddRule(rule);
                }
            } else if (line.type == Parser.LineType.Action) {
                Action action = this.GetAction(line.binding);
                string[] vars = TokenToVariables(line.binding.tokens);
                Rule rule = this.WalkRule(line, vars, false);
                string payloadID = System.Guid.NewGuid().ToString();
                action.AddPayload(rule, payloadID);
                this.actionPayloads[payloadID] = line.children.ToArray();
            } else if (line.type == Parser.LineType.Exclusion) {
                Exclusion exclusion = this.CreateExclusion(line);
                Debug.Log("Adding exclusion to " + line.binding.key);
                this.exclusions[line.binding.key] = exclusion;
            }

            foreach (Parser.Line child in line.children) {
                this.LoadLine(child);
            }
        }

        private Exclusion CreateExclusion(Parser.Line line) {
            Exclusion exclusion = new Exclusion();
            exclusion.relationKey = line.binding.key;
            bool[] boundVars = new bool[line.binding.tokens.Length];

            string[] vars = TokenToVariables(line.binding.tokens);
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

        private Rule WalkRule(Parser.Line line, string[] vars, bool salt = true) {
            Rule rule = new Rule(vars, salt);
            
            Parser.Line parent = line.parent;
            while (parent != null) {
                Relation condRelation = this.GetRelation(parent.binding);
                string[] condVars = TokenToVariables(parent.binding.tokens);
                rule.AddCondition(condRelation, condVars);
                parent = parent.parent;
            }
            return rule;
        }

        public void PerformAction(string query) {
            Parser.Binding binding = Parser.GetBinding(query);
            Action action = this.GetAction(binding);
            string[] vars = TokenToVariables(binding.tokens);
            string outKey = "._" + System.Guid.NewGuid().ToString();
            foreach (Stream stream in action.GetGoal(vars, outKey).Walk(new Stream())) {
                string payloadKey = stream.Walk(outKey);
                Parser.Line[] payload = this.actionPayloads[payloadKey];
                foreach (Parser.Line line in payload) {
                    if (line.type == Parser.LineType.Callback) {
                        string name = Regex.Match(line.content, "^([a-zA-Z0-9]+):").Groups[1].Value;
                        string[] variables = this.WalkTokens(line.binding.tokens, stream);
                        this.DoCallback(name, variables);
                    }
                }
            }
        }

        private string[] WalkTokens(Parser.Token[] tokens, Stream stream) {
            string[] output = new string[tokens.Length];
            for (int i = 0; i < tokens.Length; i++) {
                Parser.Token token = tokens[i];
                string tokenValue = token.type == Parser.TokenType.Variable ? "._" + token.value : token.value;
                output[i] = stream.Walk(tokenValue);
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

        private static string[] TokenToVariables(Parser.Token[] tokens) {
            string[] output = new string[tokens.Length];
            for (int i = 0; i < output.Length; i++) {
                Parser.Token token = tokens[i];
                if (token.type == Parser.TokenType.Variable) {
                    output[i] = "._" + token.value;
                } else {
                    output[i] = token.value;
                }
            }
            return output;
        }

        public bool IsTrue(string query) {
            Parser.Binding binding = Parser.GetBinding(query);
            Relation relation = this.relations[binding.key];
            string[] vars = TokenToVariables(binding.tokens);
            Stream stream = new Stream();
            foreach (Stream walkStream in relation.Query(vars).Walk(stream)) {
                return true;
            }
            return false;
        }
    }
}
