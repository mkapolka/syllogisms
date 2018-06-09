using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Syllogisms {
    public class InvalidRelationException : System.Exception {
        public InvalidRelationException(string message) : base(message) {}
    }

    public struct Pair {
        public string key;
        public bool isVariable;

        public Pair(string key, bool isVariable) {
            this.key = key;
            this.isVariable = isVariable;
        }

        public static Pair Fresh() {
            return new Pair(System.Guid.NewGuid().ToString(), true);
        }

        public static Pair Variable(string key) {
            return new Pair(key, true);
        }

        public static Pair Value(string v) {
            return new Pair(v, false);
        }

        public static Pair[] Freshes(string[] names) {
            Pair[] output = new Pair[names.Length];
            for (int i = 0; i < names.Length; i++) {
                output[i] = new Pair(names[i], true);
            }
            return output;
        }

        public static Pair[] Values(string[] values) {
            Pair[] output = new Pair[values.Length];
            for (int i = 0; i < values.Length; i++) {
                output[i] = new Pair(values[i], false);
            }
            return output;
        }
    }

    public class Stream {
        public Dictionary<string, Pair> replacements;

        public Stream(Dictionary<string, Pair> oldReplacements) {
            this.replacements = new Dictionary<string, Pair>(oldReplacements);
        }

        public Stream() {
            this.replacements = new Dictionary<string, Pair>();
        }

        public static bool IsVariable(Pair s) {
            return s.isVariable;
        }

        public string Walk(string var) {
            Pair p = new Pair(var, true);
            Pair result = this.Walk(p);
            if (result.isVariable) {
                return null;
            } else {
                return result.key;
            }
        }

        public Pair Walk(Pair var) {
            if (Stream.IsVariable(var)) {
                if (this.replacements.ContainsKey(var.key)) {
                    return this.Walk(this.replacements[var.key]);
                }
            }
            return var;
        }

        public Stream AddAssociation(string key, string value) {
            Pair v = new Pair(value, false);
            return this.AddAssociation(key, v);
        }

        public Stream AddAssociation(Pair key, Pair value) {
            return this.AddAssociation(key.key, value);
        }

        public Stream AddAssociation(string key, Pair value) {
            Stream output = new Stream(this.replacements);
            output.replacements.Add(key, value);
            return output;
        }
    }

    public interface Goal {
        IEnumerable<Stream> Walk (Stream input);
    }

    public class Eq : Goal {
        private Pair a;
        private Pair b;

        public Eq(Pair a, Pair b) {
            this.a = a;
            this.b = b;
        }

        public IEnumerable<Stream> Walk (Stream input) {
          Pair a_rei = input.Walk(this.a);
          Pair b_rei = input.Walk(this.b);

          if (Stream.IsVariable(a_rei)) {
              Stream a = input.AddAssociation(a_rei, b_rei);
              if (a != null) {
                  yield return a;
              }
          } else if (Stream.IsVariable(b_rei)) {
              Stream a = input.AddAssociation(b_rei, a_rei);
              if (a != null) {
                  yield return a;
              }
          } else if (a_rei.key == b_rei.key) {
              yield return input;
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
                return new Eq(Pair.Value("true"), Pair.Value("true"));
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
                return new Eq(Pair.Value("true"), Pair.Value("false"));
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
            private Pair[] variables;
            public LazyRelationGoal(Relation relation, Pair[] variables) {
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

        private List<Pair[]> facts = new List<Pair[]>();
        private List<Rule> rules = new List<Rule>();

        public void AddFact(Pair[] fact) {
            this.facts.Add(fact);
        }

        public void AddFact(string[] fact) {
            Pair[] pfact = Pair.Values(fact);
            this.facts.Add(pfact);
        }

        private bool FactsMatch(string[] a, Pair[] b) {
            for (int i = 0; i < a.Length; i++) {
                if (a[i] != b[i].key) {
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

        private Goal GetLinewiseGoal(Pair[] variables, Pair[] fact) {
            Eq[] eqs = new Eq[variables.Length];
            for (int i = 0; i < variables.Length; i++) {
                eqs[i] = new Eq(variables[i], fact[i]);
            }
            return Conj.Conjs(eqs);
        }

        public Goal Query(Pair[] variables) {
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

        public Goal GetGoal(Pair[] variables) {
            Goal[] lineWise = new Goal[this.facts.Count];
            for (int i = 0; i < this.facts.Count; i++) {
                lineWise[i] = this.GetLinewiseGoal(variables, this.facts[i]);
            }
            return Disj.Disjs(lineWise);
        }
    }

    public class Rule {
        private string salt;
        private Pair[] vars;
        private List<Goal> conditions = new List<Goal>();
        public List<Relation> relations = new List<Relation>();

        public Rule(Pair[] vars, bool salt = true) {
            if (salt) {
                this.salt = System.Guid.NewGuid().ToString();
            } else {
                this.salt = "";
            }
            this.vars = this.SaltVars(vars);
        }

        public void AddCondition(Relation condition, Pair[] vars) {
            this.conditions.Add(condition.Query(this.SaltVars(vars)));
            this.relations.Add(condition);
        }

        public Goal GetGoal(Pair[] variables) {
            List<Goal> goals = new List<Goal>();
            for (int i = 0; i < variables.Length; i++) {
                goals.Add(new Eq(variables[i], this.vars[i]));
            }

            foreach (Goal condition in this.conditions) {
                goals.Add(condition);
            }

            return Conj.Conjs(goals.ToArray());
        }

        private Pair[] SaltVars(Pair[] vars) {
            Pair[] output = new Pair[vars.Length];
            for (int i = 0; i < vars.Length; i++) {
                Pair var = vars[i];
                if (var.isVariable) {
                    output[i] = new Pair(this.salt + var.key, true);
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

        public Goal GetGoal(Pair[] vars, Pair outKey) {
            Goal[] goals = new Goal[this.rules.Count];
            for (int i = 0; i < goals.Length; i++) {
                Pair payloadPair = Pair.Value(this.payloads[i]);
                goals[i] = new Conj(this.rules[i].GetGoal(vars), new Eq(outKey, payloadPair));
            }
            return Disj.Disjs(goals);
        }

        public void AddPayload(Rule rule, string payload) {
            this.rules.Add(rule);
            this.payloads.Add(payload);
        }

        public string GetPayload(string[] vars) {
            Pair[] freshes = Pair.Freshes(vars);
            Pair key = Pair.Fresh();
            foreach (Stream walkStream in this.GetGoal(freshes, key).Walk(new Stream())) {
                return walkStream.Walk(key).key;
            }
            return null;
        }
    }
}
