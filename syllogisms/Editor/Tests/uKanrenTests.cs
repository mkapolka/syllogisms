using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

using Syllogisms;

public class uKanrenTests {

    public static void AssertResults(string[,,] values, Goal goal, Stream stream = null) {
        if (stream == null) {
            stream = new Stream();
        }
        int results = 0;
        foreach (Stream walkStream in goal.Walk(stream)) {
            bool anyMatch = false;
            for (int i = 0; i < values.GetLength(0); i++) {
                bool allMatch = true;
                for (int j = 0; j < values.GetLength(1); j++) {
                    if (walkStream.Walk(values[i,j,0]) != values[i,j,1]) {
                        allMatch = false;
                    }
                }
                if (allMatch) {
                    anyMatch = true;
                }
            }
            if (!anyMatch) {
                Assert.Fail("Walked value not found in expected results.");
            }
            results++;
        }
        Assert.AreEqual(values.GetLength(0), results);
    }

    [Test]
    public void StreamWalkTests() {
        Stream stream = new Stream();

        // Const value
        Assert.AreEqual(Pair.Value("test"), stream.Walk(Pair.Value("test")));

        // Variable -> Value
        Pair a = Pair.Fresh();
        Stream newStream = stream.AddAssociation(a, Pair.Value("test"));
        Assert.AreEqual(newStream.Walk(a.key), "test");

        // Variable -> Assigned variable
        Pair b = Pair.Fresh();
        newStream = newStream.AddAssociation(b, a);
        Assert.AreEqual(newStream.Walk(b.key), "test");

        // Variable -> Unassigned variable
        Stream newStream2 = new Stream();
        newStream2 = stream.AddAssociation(a, b);
        Assert.AreEqual(newStream2.Walk(a), b);
    }

    [Test]
    public void EqTests() {
        Pair a = Pair.Variable("a");
        Goal g = new Eq(a, Pair.Value("the letter a"));
        AssertResults(new string[,,] {
            {
                {"a", "the letter a"}
            }
        }, g);
    }

    [Test]
    public void ConjTests() {
        Pair a = Pair.Variable("a");
        Pair b = Pair.Variable("b");
        Goal g = new Conj(new Eq(a, Pair.Value("the letter a")), new Eq(b, Pair.Value("the letter b")));
        AssertResults(new string[,,] {
            {
                {"a", "the letter a"},
                {"b", "the letter b"}
            }
        }, g);

        g = new Conj(new Eq(a, Pair.Value("the letter a")), new Eq(b, a));
        AssertResults(new string[,,] {
            {
                {"a", "the letter a"},
                {"b", "the letter a"}
            }
        }, g);
    }

    [Test]
    public void EmptyConjTest() {
        Pair a = Pair.Variable("a");
        Goal g = new Conj(new Eq(a, Pair.Value("1")), new Eq(a, Pair.Value("2")));
        AssertResults(new string[0,0,0], g);

        g = new Conj(
            new Disj(new Eq(a, Pair.Value("1")), new Eq(a, Pair.Value("2"))),
            new Eq(a, Pair.Value("3"))
        );
        AssertResults(new string[0,0,0], g);
    }

    [Test]
    public void DisjTests() {
        Pair a = Pair.Variable("a");
        Goal g = new Disj(new Eq(a, Pair.Value("the letter a")), new Eq(a, Pair.Value("the letter b")));
        AssertResults(new string[,,] {
            {
                {"a", "the letter a"}
            },
            {
                {"a", "the letter b"}
            }
        }, g);
    }

    [Test]
    public void TestConjDisj() {
        Pair a = Pair.Variable("a");
        Pair b = Pair.Variable("b");
        Goal g = new Disj(
            new Conj(new Eq(a, Pair.Value("the letter a")), new Eq(b, Pair.Value("the letter b"))),
            new Eq(a, Pair.Value("Another thing entirely"))
        );
        AssertResults(new string[,,] {
            {
                {"a", "the letter a"},
                {"b", "the letter b"}
            },
            {
                {"a", "Another thing entirely"},
                {"b", null}
            }
        }, g);
    }

    [Test]
    public void RelationTests() {
        Relation r = new Relation();
        r.AddFact(new string[]{"hello"});
        AssertResults(new string[,,] {
            {
                {"a", "hello"}
            }
        }, r.Query(new Pair[]{Pair.Variable("a")}));

        r.AddFact(new string[]{"dumbo"});
        AssertResults(new string[,,] {
            {
                {"a", "hello"}
            },
            {
                {"a", "dumbo"}
            }
        }, r.Query(new Pair[]{Pair.Variable("a")}));
    }

    [Test]
    public void RelationMultiVariableTests() {
        Relation r = new Relation();
        r.AddFact(new string[]{"ms", "sappy"});
        r.AddFact(new string[]{"mr", "happy"});
        AssertResults(new string[,,] {
            {
                {"a", "ms"},
                {"b", "sappy"}
            },
            {
                {"a", "mr"},
                {"b", "happy"}
            }
        }, r.Query(new Pair[]{Pair.Variable("a"), Pair.Variable("b")}));
    }

    [Test]
    public void RelationRuleAssocTest() {
        // r is true wherever r2 is true
        Relation r = new Relation();
        Relation r2 = new Relation();
        Pair a = Pair.Variable("a");
        Rule rule = new Rule(new Pair[]{a});
        rule.AddCondition(r2, new Pair[]{a});
        r.AddRule(rule);

        r2.AddFact(new string[]{"test"});

        AssertResults(new string[,,] {
            {
                {"a", "test"}
            }
        }, r.Query(new Pair[]{Pair.Variable("a")}));

        r2.AddFact(new string[]{"test 2"});
        AssertResults(new string[,,] {
            {
                {"a", "test"}
            },
            {
                {"a", "test 2"}
            }
        }, r.Query(new Pair[]{Pair.Variable("a")}));
    }

    [Test]
    public void RelationRuleFlippyTest() {
        // r(x, y) is true wherever r2(y, x) is true
        Relation r = new Relation();
        Relation r2 = new Relation();
        Pair[] norm_args = new Pair[]{Pair.Variable("x"), Pair.Variable("y")};
        Pair[] flip_args = new Pair[]{Pair.Variable("y"), Pair.Variable("x")};
        Rule rule = new Rule(norm_args);
        rule.AddCondition(r2, flip_args);
        r.AddRule(rule);

        r2.AddFact(new string[]{"y1", "x1"});
        AssertResults(new string[,,] {
            {
                {"x", "x1"},
                {"y", "y1"}
            }
        }, r.Query(norm_args));
    }

    [Test]
    public void RelationRuleComboTest() {
        // r(x) is true if r2(x) and r3(x) are true
        Relation r = new Relation();
        Relation r2 = new Relation();
        Relation r3 = new Relation();
        Pair[] args = new Pair[]{Pair.Variable("x")};
        Rule rule = new Rule(args);
        rule.AddCondition(r2, args);
        rule.AddCondition(r3, args);

        r.AddRule(rule);

        r2.AddFact(new string[]{"testy"});
        r3.AddFact(new string[]{"problemo"});

        AssertResults(new string[0,0,0], r.Query(args));

        // add "testy" to r3 and try again
        r3.AddFact(new string[]{"testy"});
        AssertResults(new string[,,] {
            {
                {"x", "testy"}
            }
        }, r.Query(args));
    }

    [Test]
    public void RelationRuleMultiOptionTest() {
        Relation r = new Relation();
        Relation r2 = new Relation();
        Relation r3 = new Relation();

        // r(x) is true if either r2(x) or r3(x)
        Pair[] args1 = new Pair[]{Pair.Fresh()};
        Rule rule = new Rule(args1);
        rule.AddCondition(r2, args1);

        Pair[] args2 = new Pair[]{Pair.Fresh()};
        Rule rule2 = new Rule(args2);
        rule2.AddCondition(r3, args2);

        r.AddRule(rule);
        r.AddRule(rule2);

        r2.AddFact(new string[]{"r2 testy"});
        r3.AddFact(new string[]{"r3 testy"});

        AssertResults(new string[,,] {
            {
                {"a", "r2 testy"}
            },
            {
                {"a", "r3 testy"}
            }
        }, r.Query(new Pair[]{Pair.Variable("a")}));
    }

    [Test]
    public void RelationRuleRecursiveTest() {
        Relation r = new Relation();
        Pair[] args = new Pair[]{Pair.Fresh()};
        Rule rule = new Rule(args);
        rule.AddCondition(r, args);
        try {
            r.AddRule(rule);
            Assert.Fail("Adding a recursive rule should raise an exception");
        } catch (InvalidRelationException) {
            //
        }
    }

    [Test]
    public void RuleTest() {
        Relation r1 = new Relation();
        r1.AddFact(new string[]{"test"});

        Pair[] args = new Pair[]{Pair.Variable("a"), Pair.Variable("b")};
        Rule rule = new Rule(args);

        rule.AddCondition(r1, new Pair[]{Pair.Variable("a")});

        AssertResults(new string[,,] {
            {
                {"a", "test"},
                {"b", null}
            }
        }, rule.GetGoal(new Pair[]{Pair.Variable("a"), Pair.Variable("b")}));
    }

    [Test]
    public void RulePartiallyBoundTest() {
        Relation r1 = new Relation();
        r1.AddFact(new string[]{"chimpy"});
        Pair[] args = new Pair[]{Pair.Variable("a"), Pair.Variable("b")};
        Rule rule = new Rule(args);

        rule.AddCondition(r1, new Pair[]{Pair.Variable("a")});

        AssertResults(new string[,,] {
            {
                {"b", null}
            }
        }, rule.GetGoal(new Pair[]{Pair.Value("chimpy"), Pair.Variable("b")}));

        foreach (Stream stream in rule.GetGoal(new Pair[]{Pair.Value("bongo"), Pair.Variable("b")}).Walk(new Stream())) {
            Pair bong = stream.Walk(new Pair("bongo", false));
        }
        AssertResults(new string[0,0,0], rule.GetGoal(new Pair[]{Pair.Value("bongo"), Pair.Variable("b")}));
    }

    /*[Test]
    public void ActionTest() {
        // Action with no conditions
        Action action = new Action();

        Rule rule = new Rule(new string[0]);
        action.AddPayload(rule, "test");

        Assert.AreEqual("test", action.GetPayload(new string[0]));

        // Action with one condition
        Relation r = new Relation();
        rule.AddCondition(r, new string[0]);

        // Now it has a condition that isn't satisfied because the relation isn't true
        Assert.AreEqual(null, action.GetPayload(new string[0]));

        r.AddFact(new string[0]);
        // Now that the relation is fulfilled, the action has a payload again.
        Assert.AreEqual("test", action.GetPayload(new string[0]));
    }

    [Test]
    public void ActionMultiTest() {
        // :: cond a
        //      * Action
        // :: cond b
        //      * Action
        Action action = new Action();

        Rule rule1 = new Rule(new string[0]);
        Relation r1 = new Relation();
        rule1.AddCondition(r1, new string[0]);

        action.AddPayload(rule1, "test A");

        Rule rule2 = new Rule(new string[0]);
        Relation r2 = new Relation();
        rule2.AddCondition(r2, new string[0]);

        action.AddPayload(rule2, "test B");

        // First there are no payloads because neither relation is true
        Assert.AreEqual(null, action.GetPayload(new string[0]));

        // Add a fact to first relation, get first payload
        r1.AddFact(new string[0]);
        Assert.AreEqual("test A", action.GetPayload(new string[0]));

        //Add a fact to second relation, get both payloads
        r2.AddFact(new string[0]);

        int results = 0;
        foreach (Stream walkStream in action.GetGoal(new string[0], "._payload").Walk(new Stream())) {
            string _payload = walkStream.Walk("._payload");
            Assert.Contains(_payload, new string[]{"test A", "test B"});
            results++;
        }
        Assert.AreEqual(2, results);
    }

    [Test]
    public void ActionWithBindTest() {
        // :: cond (a)
        //     * Action (a)
        // :: other cond (b)
        //     * Action (b)
        Action action = new Action();

        Rule rule1 = new Rule(new string[]{"._a"});
        Relation r1 = new Relation();
        r1.AddFact(new string[]{"fact a"});
        rule1.AddCondition(r1, new string[]{"._a"});

        Rule rule2 = new Rule(new string[]{"._b"});
        Relation r2 = new Relation();
        r2.AddFact(new string[]{"fact b"});
        rule2.AddCondition(r2, new string[]{"._b"});

        action.AddPayload(rule1, "test A");
        action.AddPayload(rule2, "test B");

        Assert.AreEqual("test A", action.GetPayload(new string[]{"fact a"}));
        Assert.AreEqual("test B", action.GetPayload(new string[]{"fact b"}));
        Assert.AreEqual(null, action.GetPayload(new string[]{"fact c"}));
    }

    [Test]
    public void ActionWithFewerBindsTest() {
        // :: cond (a)
        //     * Action
        Action action = new Action();

        Rule rule = new Rule(new string[0]);
        Relation r = new Relation();
        r.AddFact(new string[]{"fact a"});
        rule.AddCondition(r, new string[]{"._a"});

        action.AddPayload(rule, "test A");

        Assert.AreEqual("test A", action.GetPayload(new string[0]));
    }*/
}
