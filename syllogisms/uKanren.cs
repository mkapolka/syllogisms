using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

    public struct Stream {
        private struct Entry {
            public string key;
            public Pair value;

            public Entry (string key, Pair value) {
                this.key = key;
                this.value = value;
            }
        }

        private Entry[] entries;

        private Stream(Stream parent, int extraCapacity) {
            if (parent.entries != null) {
                this.entries = new Entry[parent.entries.Length + extraCapacity];
                System.Array.Copy(parent.entries, this.entries, parent.entries.Length);
            } else {
                this.entries = new Entry[extraCapacity];
            }
        }

        public static bool IsVariable(Pair s) {
            return s.isVariable;
        }

        private Pair GetPair(string key) {
            if (this.entries == null) throw new System.Exception("No entries!");
            foreach (Entry entry in this.entries) {
                if (entry.key == key) {
                    return entry.value;
                }
            }
            throw new System.Exception("I don't have key " + key);
        }

        private bool HasKey(string key) {
            if (this.entries == null) return false;
            foreach (Entry entry in this.entries) {
                if (entry.key == key) {
                    return true;
                }
            }
            return false;
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
                if (this.HasKey(var.key)) {
                    return this.Walk(this.GetPair(var.key));
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
            Stream output = new Stream(this, 1);
            int nMyEntries = this.entries != null ? this.entries.Length : 0;
            output.entries[nMyEntries] = new Entry(key, value);
            return output;
        }

        public Stream AddAssociations(Pair[] variables, Pair[] values) {
            int extraEntries = 0;
            for (int i = 0; i < variables.Length; i++) {
                if (variables[i].isVariable) {
                    extraEntries += 1;
                }
            }
            Stream output = new Stream(this, extraEntries);
            int j = 0;
            int nMyEntries = this.entries != null ? this.entries.Length : 0;
            for (int i = 0; i < variables.Length; i++) {
                if (variables[i].isVariable) {
                    output.entries[nMyEntries + j] = new Entry(variables[i].key, values[i]);
                    j++;
                }
            }
            return output;
        }
    }

    public interface Goal {
        IEnumerable<Stream> Walk (Stream input);
    }

    public class Eqs : Goal {
        public Pair[] a;
        public Pair[] b;

        public Eqs(Pair[] a, Pair[] b) {
            this.a = a;
            this.b = b;
        }

        public IEnumerable<Stream> Walk(Stream input) {
            bool willUnify = true;
            Pair[] walkedVars = new Pair[this.a.Length];
            Pair[] walkedVals = new Pair[this.b.Length];
            for (int i = 0; i < this.a.Length; i++) {
                Pair a = input.Walk(this.a[i]);
                Pair b = input.Walk(this.b[i]);
                if (a.isVariable) {
                    walkedVars[i] = a;
                    walkedVals[i] = b;
                } else if (b.isVariable) {
                    walkedVars[i] = b;
                    walkedVals[i] = a;
                } else if (a.key != b.key) {
                    willUnify = false;
                    break;
                }
            }
            if (willUnify) {
                yield return input.AddAssociations(walkedVars, walkedVals);
            }
        }
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
              yield return input.AddAssociation(a_rei, b_rei);
          } else if (Stream.IsVariable(b_rei)) {
              yield return input.AddAssociation(b_rei, a_rei);
          } else if (a_rei.key == b_rei.key) {
              yield return input;
          }
        }
    }

    public class Conjs : Goal {
        public Goal[] goals;
        public Conjs(Goal[] goals) {
            this.goals = goals;
        }

        public IEnumerable<Stream> Walk(Stream input) {
            return WalkConjs(this.goals, input);
        }

        public static IEnumerable<Stream> WalkConjs(Goal[] goals, Stream input) {
            IEnumerator<Stream>[] enumerables = new IEnumerator<Stream>[goals.Length];
            Stream[] streams = new Stream[goals.Length];

            if (goals.Length == 0) {
                yield return input;
            } else {
                streams[0] = input;
                enumerables[0] = goals[0].Walk(input).GetEnumerator();
                int i = 0;
                bool done = false;
                while (i >= 0) {
                    bool more = enumerables[i].MoveNext();
                    if (more) {
                        if (i == goals.Length - 1) {
                            yield return enumerables[i].Current;
                        } else {
                            streams[i] = enumerables[i].Current;
                            enumerables[i+1] = goals[i+1].Walk(streams[i]).GetEnumerator();
                            i++;
                        }
                    } else {
                        i--;
                    }
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

        public IEnumerable<Stream> Walk (Stream input) {
            foreach (Stream streamA in this.a.Walk(input)) {
                foreach (Stream streamB in this.b.Walk(streamA)) {
                    yield return streamB;
                }
            }
        }
    }

    public class Disjs : Goal {
        private Goal[] goals;
        private bool[] done;

        public Disjs(Goal[] goals) {
            this.goals = goals;
            this.done = new bool[this.goals.Length];
        }

        public IEnumerable<Stream> Walk(Stream input) {
            IEnumerator<Stream>[] streams = new IEnumerator<Stream>[this.goals.Length];
            for (int i = 0; i < this.done.Length; i++) {
                this.done[i] = false;
                streams[i] = this.goals[i].Walk(input).GetEnumerator();
            }

            int nDone = 0;
            while (nDone < this.done.Length) {
                for (int i = 0; i < this.goals.Length; i++) {
                    if (!this.done[i]) {
                        bool isDone = !streams[i].MoveNext();
                        if (isDone) {
                            this.done[i] = isDone;
                            nDone++;
                        } else {
                            yield return streams[i].Current;
                        }
                    }
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
            private bool rules;

            public LazyRelationGoal(Relation relation, Pair[] variables, bool rules=true) {
                this.relation = relation;
                this.variables = variables;
                this.rules = rules;
            }

            public IEnumerable<Stream> Walk (Stream input) {
                // Check the facts
                foreach (Pair[] fact in this.relation.facts) {
                    foreach (Stream stream in new Eqs(this.variables, fact).Walk(input)) {
                        yield return stream;
                    }
                }

                if (this.rules) {
                    foreach (Rule rule in this.relation.rules) {
                        foreach (Stream stream in rule.GetGoal(this.variables).Walk(input)) {
                            yield return stream;
                        }
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
                if (this.FactsMatch(fact, this.facts[i])) {
                    this.facts.RemoveAt(i);
                    i--;
                }
            }
        }

        public Goal Query(Pair[] variables, bool rules=true) {
            return new LazyRelationGoal(this, variables, rules);
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

    }


    public class Rule {
        private class RuleGoal : Goal {
            private Rule rule;
            private Pair[] vars;

            private Goal eq;

            public RuleGoal(Pair[] vars, Rule rule) {
                this.vars = vars;
                this.rule = rule;

                this.eq = new Eqs(vars, rule.vars);
            }

            public IEnumerable<Stream> Walk(Stream input) {
                foreach (Stream stream in this.eq.Walk(input)) {
                    return Conjs.WalkConjs(this.rule.conditions.ToArray(), input);
                }
                return Enumerable.Empty<Stream>();
            }
        }

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
            goals.Add(new Eqs(variables, this.vars));

            foreach (Goal condition in this.conditions) {
                goals.Add(condition);
            }

            return new Conjs(goals.ToArray());
        }

        public bool IsTrue(Pair[] variables) {
            foreach (Stream stream in this.GetGoal(variables).Walk(new Stream())) {
                return true;
            }
            return false;
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
            return new Disjs(goals);
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
