using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Syllogisms {

    public class InvalidRelationException : System.Exception {
        public InvalidRelationException(string message) : base(message) {}
    }

    public class Stream {
        public Dictionary<string, string> replacements;

        public Stream(Dictionary<string, string> oldReplacements) {
            this.replacements = new Dictionary<string, string>(oldReplacements);
        }

        public Stream() {
            this.replacements = new Dictionary<string, string>();
        }

        public static bool IsVariable(string s) {
            return s.StartsWith("._");
        }

        public string Walk(string var) {
            if (Stream.IsVariable(var)) {
                if (this.replacements.ContainsKey(var)) {
                    return this.Walk(this.replacements[var]);
                }
            }
            return var;
        }

        public Stream AddAssociation(string key, string value) {
            Stream output = new Stream(this.replacements);
            string v2 = this.Walk(value);
            if (v2 != key) {
                output.replacements.Add(key, v2);
                return output;
            } else {
                return null;
            }
        }
    }

    public interface Goal {
        IEnumerable<Stream> Walk (Stream input);
    }

    public class Eq : Goal {
        private string a;
        private string b;

        public Eq(string a, string b) {
            this.a = a;
            this.b = b;
        }

        public IEnumerable<Stream> Walk (Stream input) {
          string a_rei = input.Walk(this.a);
          string b_rei = input.Walk(this.b);

          if (a_rei == b_rei) {
              yield return input;
          } else if (Stream.IsVariable(a_rei)) {
              Stream a = input.AddAssociation(a_rei, b_rei);
              if (a != null) {
                  yield return a;
              }
          } else if (Stream.IsVariable(b_rei)) {
              Stream a = input.AddAssociation(b_rei, a_rei);
              if (a != null) {
                  yield return a;
              }
          }
        }
    }

    public class Conj : Goal {
        public Goal a;
        public Goal b;
        public Conj(Goal a, Goal b) {
            this.a = a;
            this.b = b;
        }

        public static Goal Conjs(Goal[] goals) {
            if (goals.Length == 0) {
                return new Eq("true", "true");
            }

            if (goals.Length == 1) {
                return goals[0];
            }
            Conj conj = new Conj(goals[0], goals[1]);
            int i = 2;
            for (; i < goals.Length; i++) {
                conj = new Conj(conj, goals[i]);
            }
            return conj;
        }


        public IEnumerable<Stream> Walk (Stream input) {
            foreach (Stream streamA in this.a.Walk(input)) {
                foreach (Stream streamB in this.b.Walk(streamA)) {
                    yield return streamB;
                }
            }
        }
    }

    public class Disj : Goal {
        public Goal a;
        public Goal b;

        public Disj(Goal a, Goal b) {
            this.a = a;
            this.b = b;
        }

        public static Goal Disjs(Goal[] goals) {
            if (goals.Length == 0) {
                return new Eq("true", "false");
            }

            if (goals.Length == 1) {
                return goals[0];
            }
            Disj disj = new Disj(goals[0], goals[1]);
            int i = 2;
            for (; i < goals.Length; i++) {
                disj = new Disj(disj, goals[i]);
            }
            return disj;
        }

        public IEnumerable<Stream> Walk (Stream input) {
            bool aDone = false;
            bool bDone = false;
            IEnumerator<Stream> aStream = this.a.Walk(input).GetEnumerator();
            IEnumerator<Stream> bStream = this.b.Walk(input).GetEnumerator();
            while (!aDone && !bDone) {
                if (!aDone) {
                    aDone = !aStream.MoveNext();
                    if (!aDone) {
                        yield return aStream.Current;
                    }
                }

                if (!bDone) {
                      bDone = !bStream.MoveNext();
                      if (!bDone) {
                            yield return bStream.Current;
                      }
                }
            }
        }
    }

    public class Relation {
        private class LazyRelationGoal : Goal {
            private Relation relation;
            private string[] variables;
            public LazyRelationGoal(Relation relation, string[] variables) {
                this.relation = relation;
                this.variables = variables;
            }

            public IEnumerable<Stream> Walk (Stream input) {
                Goal goal = this.relation.GetGoal(this.variables);

                foreach (Stream stream in goal.Walk(input)) {
                    yield return stream;
                }

                foreach (Rule rule in this.relation.rules) {
                    foreach (Stream stream in rule.GetGoal(this.variables).Walk(input)) {
                        yield return stream;
                    }
                }
            }
        }

        private List<string[]> facts = new List<string[]>();
        private List<Rule> rules = new List<Rule>();

        public void AddFact(string[] fact) {
            this.facts.Add(fact);
        }

        private bool FactsMatch(string[] a, string[] b) {
            for (int i = 0; i < a.Length; i++) {
                if (a[i] != b[i]) {
                    return false;
                }
            }
            return true;
        }

        public void RemoveFact(string[] fact) {
            for (int i = 0; i < this.facts.Count; i++) {
                if (!this.FactsMatch(fact, this.facts[i])) {
                    this.facts.RemoveAt(i);
                    i--;
                }
            }
        }

        private Goal GetLinewiseGoal(string[] variables, string[] fact) {
            Eq[] eqs = new Eq[variables.Length];
            for (int i = 0; i < variables.Length; i++) {
                eqs[i] = new Eq(variables[i], fact[i]);
            }
            return Conj.Conjs(eqs);
        }

        public Goal Query(string[] variables) {
            return new LazyRelationGoal(this, variables);
        }

        private bool CheckRecursiveRules(Rule rule) {
            foreach (Relation relation in rule.relations) {
                if (relation == this) {
                    return false;
                } else {
                    foreach (Rule rule2 in relation.rules) {
                        bool check = this.CheckRecursiveRules(rule2);
                        if (!check) {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public void AddRule(Rule rule) {
            // Check for recursive rules
            if (!this.CheckRecursiveRules(rule)) {
                throw new InvalidRelationException("Recursive rule found.");
            }
            this.rules.Add(rule);
        }

        public Goal GetGoal(string[] variables) {
            Goal[] lineWise = new Goal[this.facts.Count];
            for (int i = 0; i < this.facts.Count; i++) {
                lineWise[i] = this.GetLinewiseGoal(variables, this.facts[i]);
            }
            return Disj.Disjs(lineWise);
        }
    }

    public class Rule {
        private string salt;
        private string[] vars;
        private List<Goal> conditions = new List<Goal>();
        public List<Relation> relations = new List<Relation>();

        public Rule(string[] vars, bool salt = true) {
            if (salt) {
                this.salt = System.Guid.NewGuid().ToString();
            } else {
                this.salt = "";
            }
            this.vars = this.SaltVars(vars);
        }

        public void AddCondition(Relation condition, string[] vars) {
            this.conditions.Add(condition.Query(this.SaltVars(vars)));
            this.relations.Add(condition);
        }

        public Goal GetGoal(string[] variables) {
            List<Goal> goals = new List<Goal>();
            for (int i = 0; i < variables.Length; i++) {
                goals.Add(new Eq(variables[i], this.vars[i]));
            }

            foreach (Goal condition in this.conditions) {
                goals.Add(condition);
            }

            return Conj.Conjs(goals.ToArray());
        }

        private string[] SaltVars(string[] vars) {
            string[] output = new string[vars.Length];
            for (int i = 0; i < vars.Length; i++) {
                string var = vars[i];
                if (Stream.IsVariable(var)) {
                    output[i] = "._" + this.salt + var.Substring(2);
                } else {
                    output[i] = var;
                }
            }
            return output;
        }
    }

    public class Action {
        public List<Rule> rules = new List<Rule>();
        public List<string> payloads = new List<string>();

        public Goal GetGoal(string[] vars, string outKey) {
            Goal[] goals = new Goal[this.rules.Count];
            for (int i = 0; i < goals.Length; i++) {
                goals[i] = new Conj(this.rules[i].GetGoal(vars), new Eq(outKey, this.payloads[i]));
            }
            return Disj.Disjs(goals);
        }

        public void AddPayload(Rule rule, string payload) {
            this.rules.Add(rule);
            this.payloads.Add(payload);
        }

        public string GetPayload(string[] vars) {
            string uniqueString = "._" + System.Guid.NewGuid().ToString();
            foreach (Stream walkStream in this.GetGoal(vars, uniqueString).Walk(new Stream())) {
                return walkStream.Walk(uniqueString);
            }
            return null;
        }
    }
}
