using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

using Syllogisms;

public class uKanrenTests {

    [Test]
    public void TestsSimplePasses() {
        Assert.AreEqual(1, 1);
    }

    [Test]
    public void IsVariable() {
        Assert.AreEqual(Stream.IsVariable("._a"), true);
        Assert.AreEqual(Stream.IsVariable("._b"), true);
        Assert.AreEqual(Stream.IsVariable("stringy"), false);
    }

    [Test]
    public void StreamWalkTests() {
        Stream stream = new Stream();

        // Unassigned variable
        Assert.AreEqual(stream.Walk("._a"), "._a");

        // Variable -> Value
        Stream newStream = stream.AddAssociation("._a", "test");
        Assert.AreEqual(newStream.Walk("._a"), "test");

        // Variable -> Assigned variable
        newStream = newStream.AddAssociation("._b", "._a");
        Assert.AreEqual(newStream.Walk("._b"), "test");

        // Variable -> Unassigned variable
        newStream = stream.AddAssociation("._a", "._b");
        Assert.AreEqual(newStream.Walk("._a"), "._b");
    }

    [Test]
    public void EqTests() {
        Stream stream = new Stream();
        Goal g = new Eq("._a", "the letter a");
        int results = 0;
        foreach (Stream walkedStream in g.Walk(stream)) {
            results++;
            Assert.AreEqual(walkedStream.Walk("._a"), "the letter a");
        }
        Assert.AreEqual(results, 1);
    }

    [Test]
    public void OccursTest() {
        Stream stream = new Stream();
        Stream a = stream.AddAssociation("._a", "._b");
        Assert.IsNotNull(a);
        Stream b = a.AddAssociation("._b", "._a");
        Assert.IsNull(b);
    }

    [Test]
    public void ConjTests() {
        Stream stream = new Stream();
        Goal g = new Conj(new Eq("._a", "the letter a"), new Eq("._b", "the letter b"));
        int results = 0;
        foreach (Stream walkedStream in g.Walk(stream)) {
            results++;
            Assert.AreEqual("the letter a", walkedStream.Walk("._a"));
            Assert.AreEqual("the letter b", walkedStream.Walk("._b"));
        }
        Assert.AreEqual(results, 1);

        g = new Conj(new Eq("._a", "the letter a"), new Eq("._b", "._a"));
        results = 0;
        foreach (Stream walkedStream in g.Walk(stream)) {
            results++;
            Assert.AreEqual("the letter a", walkedStream.Walk("._a"));
            Assert.AreEqual("the letter a", walkedStream.Walk("._b"));
        }
        Assert.AreEqual(results, 1);
    }

    [Test]
    public void EmptyConjTest() {
        Stream stream = new Stream();
        Goal g = new Conj(new Eq("._a", "1"), new Eq("._a", "2"));
        int results = 0;
        foreach (Stream walkedStream in g.Walk(stream)) {
            results++;
        }
        Assert.AreEqual(0, results);

        g = new Conj(
            new Disj(new Eq("._a", "1"), new Eq("._a", "2")),
            new Eq("._a", "3")
        );
        foreach (Stream walkedStream in g.Walk(stream)) {
            results++;
        }
        Assert.AreEqual(0, results);
    }

    [Test]
    public void DisjTests() {
        Stream stream = new Stream();
        Goal g = new Disj(new Eq("._a", "the letter a"), new Eq("._a", "the letter b"));
        int results = 0;
        foreach (Stream walkedStream in g.Walk(stream)) {
            Assert.Contains(walkedStream.Walk("._a"), new string[]{"the letter a", "the letter b"});
            results++;
        }
        Assert.AreEqual(2, results);
    }

    [Test]
    public void TestConjDisj() {
        Stream stream = new Stream();
        Goal g = new Disj(
            new Conj(new Eq("._a", "the letter a"), new Eq("._b", "the letter b")),
            new Eq("._a", "Another thing entirely")
        );
        int results = 0;
        foreach (Stream walkedStream in g.Walk(stream)) {
            string a = walkedStream.Walk("._a");
            string b = walkedStream.Walk("._b");
            Assert.Contains(a, new string[]{"the letter a", "Another thing entirely"});
            Assert.Contains(b, new string[]{"the letter b", "._b"});
            Assert.AreEqual(true, a == "the letter a" ? b == "the letter b" : b == "._b");
            results++;
        }
        Assert.AreEqual(2, results);
    }

    [Test]
    public void RelationTests() {
        Stream stream = new Stream();
        Relation r = new Relation();
        r.AddFact(new string[]{"hello"});
        int results = 0;
        foreach (Stream walkedStream in r.GetGoal(new string[]{"._a"}).Walk(stream)) {
            Assert.AreEqual("hello", walkedStream.Walk("._a"));
            results++;
        }
        Assert.AreEqual(1, results);

        r.AddFact(new string[]{"dumbo"});
        results = 0;
        foreach (Stream walkedStream in r.GetGoal(new string[]{"._a"}).Walk(stream)) {
            Assert.Contains(walkedStream.Walk("._a"), new string[]{"hello", "dumbo"});
            results++;
        }
    }

    [Test]
    public void RelationMultiVariableTests() {
        Stream stream = new Stream();
        Relation r = new Relation();
        r.AddFact(new string[]{"ms", "sappy"});
        r.AddFact(new string[]{"mr", "happy"});
        int results = 0;
        foreach (Stream walkedStream in r.GetGoal(new string[]{"._a", "._b"}).Walk(stream)) {
            results++;
            string _a = walkedStream.Walk("._a");
            if (_a == "ms") {
                Assert.AreEqual("sappy", walkedStream.Walk("._b"));
            } else if (_a == "mr") {
                Assert.AreEqual("happy", walkedStream.Walk("._b"));
            } else {
                Assert.AreEqual(false, true);
            }
        }
        Assert.AreEqual(2, results);
    }

    [Test]
    public void RelationRuleAssocTest() {
        Stream stream = new Stream();
        // r is true wherever r2 is true
        Relation r = new Relation();
        Relation r2 = new Relation();
        Rule rule = new Rule(new string[]{"._a"});
        rule.AddCondition(r2, new string[]{"._a"});
        r.AddRule(rule);

        r2.AddFact(new string[]{"test"});
        foreach (Stream walkedStream in r.Query(new string[]{"._a"}).Walk(stream)) {
            string _a = walkedStream.Walk("._a");
            Assert.AreEqual("test", _a);
        }

        r2.AddFact(new string[]{"test 2"});
        int results = 0;
        foreach (Stream walkedStream in r.Query(new string[]{"._a"}).Walk(stream)) {
            string _a = walkedStream.Walk("._a");
            Assert.Contains(_a, new string[]{"test", "test 2"});
            results++;
        }
        Assert.AreEqual(2, results);
    }

    [Test]
    public void RelationRuleFlippyTest() {
        Stream stream = new Stream();
        // r(x, y) is true wherever r2(y, x) is true
        Relation r = new Relation();
        Relation r2 = new Relation();
        Rule rule = new Rule(new string[]{"._x", "._y"});
        rule.AddCondition(r2, new string[]{"._y", "._x"});
        r.AddRule(rule);

        r2.AddFact(new string[]{"y1", "x1"});
        int results = 0;
        foreach (Stream walkedStream in r.Query(new string[]{"._a", "._b"}).Walk(stream)) {
            string _x = walkedStream.Walk("._a");
            string _y = walkedStream.Walk("._b");
            Assert.AreEqual("x1", _x);
            Assert.AreEqual("y1", _y);
            results++;
        }
        Assert.AreEqual(1, results);
    }

    [Test]
    public void RelationRuleComboTest() {
        Stream stream = new Stream();
        // r(x) is true if r2(x) and r3(x) are true
        Relation r = new Relation();
        Relation r2 = new Relation();
        Relation r3 = new Relation();
        Rule rule = new Rule(new string[]{"._x"});
        rule.AddCondition(r2, new string[]{"._x"});
        rule.AddCondition(r3, new string[]{"._x"});

        r.AddRule(rule);

        r2.AddFact(new string[]{"testy"});
        r3.AddFact(new string[]{"problemo"});

        // no value is shared between r2 and r3
        foreach (Stream walkedStream in r.Query(new string[]{"._a"}).Walk(stream)) {
            Assert.AreEqual(false, true);
        }

        // add "testy" to r3 and try again
        r3.AddFact(new string[]{"testy"});
        int results = 0;
        foreach (Stream walkedStream in r.Query(new string[]{"._a"}).Walk(stream)) {
            string _a = walkedStream.Walk("._a");
            Assert.AreEqual("testy", _a);
            results++;
        }
        Assert.AreEqual(1, results);
    }

    [Test]
    public void RelationRuleMultiOptionTest() {
        Stream stream = new Stream();
        Relation r = new Relation();
        Relation r2 = new Relation();
        Relation r3 = new Relation();

        // r(x) is true if either r2(x) or r3(x)
        Rule rule = new Rule(new string[]{"._x"});
        rule.AddCondition(r2, new string[]{"._x"});

        Rule rule2 = new Rule(new string[]{"._y"});
        rule2.AddCondition(r3, new string[]{"._y"});

        r.AddRule(rule);
        r.AddRule(rule2);

        r2.AddFact(new string[]{"r2 testy"});
        r3.AddFact(new string[]{"r3 testy"});

        int result = 0;
        foreach (Stream walkStream in r.Query(new string[]{"._a"}).Walk(stream)) {
            string _a = walkStream.Walk("._a");
            Assert.Contains(_a, new string[]{"r2 testy", "r3 testy"});
            result++;
        }
        Assert.AreEqual(2, result);
    }

    [Test]
    public void RelationRuleRecursiveTest() {
        Relation r = new Relation();
        Rule rule = new Rule(new string[]{"._x"});
        rule.AddCondition(r, new string[]{"._y"});
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

        Rule rule = new Rule(new string[]{"._a", "._b"});

        rule.AddCondition(r1, new string[]{"._a"});

        int results = 0;
        foreach (Stream stream in rule.GetGoal(new string[]{"._a", "._b"}).Walk(new Stream())) {
            string _a = stream.Walk("._a");
            string _b = stream.Walk("._b");
            Assert.AreEqual("test", _a);
            Assert.IsTrue(Stream.IsVariable(_b));
            results++;
        }
        Assert.AreEqual(1, results);
    }

    [Test]
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
    }
}
