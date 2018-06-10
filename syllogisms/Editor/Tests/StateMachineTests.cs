using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

using Syllogisms;

public class StateMachineTests {
    [Test]
    public void ClaimTest() {
        StateMachine sm = new StateMachine();
        sm.ReadFile(
            "+ condition a is true\n" +
            "+ condition b is \"true\"\n" +
            "+ condition b is \"false\"\n" +
            "+ condition b is \"neat\"\n"
        );
        Assert.IsTrue(sm.IsTrue("condition a is true"));
        Assert.IsTrue(sm.IsTrue("condition b is \"true\""));
        Assert.IsTrue(sm.IsTrue("condition b is \"false\""));
        Assert.IsTrue(sm.IsTrue("condition b is \"neat\""));
    }

    [Test]
    public void TestConditionalClaim() {
        StateMachine sm = new StateMachine();
        sm.ReadFile(
            ":: condition a is true\n" +
            "   + claim b is true\n" +
            "+ condition a is true\n"
        );
        Assert.IsTrue(sm.IsTrue("condition a is true"));
        Assert.IsTrue(sm.IsTrue("claim b is true"));
    }

    [Test]
    public void TestConditionalWithWildcards() {
        StateMachine sm = new StateMachine();
        sm.ReadFile(
            ":: condition a is (value)\n" +
            "   + claim b is (value)\n" +
            "+ condition a is \"testy\"\n"
        );
        Assert.IsTrue(sm.IsTrue("condition a is \"testy\""));
        Assert.IsTrue(sm.IsTrue("claim b is \"testy\""));
        Assert.IsFalse(sm.IsTrue("claim b is \"super\""));
    }

    [Test]
    public void TestMultiConditional() {
        StateMachine sm = new StateMachine();
        sm.ReadFile(
            ":: condition a is (value)\n" +
            "   :: condition b is (other value)\n" +
            "       + claim c is (value) and (other value)\n" +
            "+ condition a is \"testy\"\n" + 
            "+ condition b is \"zesty\"\n"
        );
        Assert.IsTrue(sm.IsTrue("condition a is \"testy\""));
        Assert.IsTrue(sm.IsTrue("condition b is \"zesty\""));
        Assert.IsTrue(sm.IsTrue("claim c is \"testy\" and \"zesty\""));
        Assert.IsFalse(sm.IsTrue("claim c is \"super\" and \"duper\""));
    }

    [Test]
    public void TestMismatchedArgCount() {
        StateMachine sm = new StateMachine();
        sm.ReadFile(
            ":: condition a is (value)\n" +
            "       + claim c is true\n" +
            "+ condition a is \"testy\"\n"
        );
        Assert.IsFalse(sm.IsTrue("condition a is \"besty\""));
        Assert.IsTrue(sm.IsTrue("condition a is \"testy\""));
        Assert.IsTrue(sm.IsTrue("claim c is true"));
    }

    [Test]
    public void TestExtraction() {
        StateMachine sm = new StateMachine();
        sm.ReadFile(
            ":: condition a is (value) and (other value)\n" +
            "   + claim b is (value)\n" +
            "   + claim c is (other value)\n" +
            "+ condition a is \"testy\" and \"zesty\"\n"
        );
        Assert.IsTrue(sm.IsTrue("condition a is \"testy\" and \"zesty\""));
        Assert.IsTrue(sm.IsTrue("claim b is \"testy\""));
        Assert.IsTrue(sm.IsTrue("claim c is \"zesty\""));

        Assert.IsFalse(sm.IsTrue("condition a is \"super\" and \"duper\""));
        Assert.IsFalse(sm.IsTrue("claim b is \"zesty\""));
        Assert.IsFalse(sm.IsTrue("claim c is \"testy\""));
    }

    [Test]
    public void ActionTest() {
        StateMachine sm = new StateMachine();
        sm.ReadFile(
            ":: condition a is (something)\n" +
            "   * Action\n" +
            "       > callback: (something) \"static\"\n" +
            "+ condition a is \"testy\"\n"
        );
        Assert.IsTrue(sm.IsTrue("condition a is \"testy\""));
        bool called = false;
        sm.AddCallback("callback", (string[] vars) => {
            called = true;
            Assert.AreEqual(2, vars.Length);
            Assert.AreEqual("testy", vars[0]);
            Assert.AreEqual("static", vars[1]);
        });
        sm.PerformAction("Action");
        Assert.IsTrue(called);
    }

    [Test]
    public void ExclusionConditionalTest() {
        StateMachine sm = new StateMachine();
        sm.ReadFile(@"
            :: condition a is (something)
                ? claim (something) is (unbound)
            + condition a is ""mojo""
        ");
        sm.Claim("claim \"mojo\" is \"great\"");
        sm.Claim("claim \"mojo\" is \"good\"");
        sm.Claim("claim \"chimpy\" is \"wonderful\"");
        sm.Claim("claim \"chimpy\" is \"exquisite\"");
        Assert.IsTrue(sm.IsTrue("claim \"mojo\" is \"good\""));
        Assert.IsFalse(sm.IsTrue("claim \"mojo\" is \"great\""));
        Assert.IsTrue(sm.IsTrue("claim \"chimpy\" is \"wonderful\""));
        Assert.IsTrue(sm.IsTrue("claim \"chimpy\" is \"exquisite\""));
    }

    [Test]
    public void ExclusionAmbiguousTest() {
        StateMachine sm = new StateMachine();
        sm.ReadFile(@"
            :: condition a is (something)
                ? claim (something) is (unbound)
            :: condition b is (something else)
                ? claim (something) is (something else)
            + condition a is ""mojo""
        ");
        sm.Claim("claim \"mojo\" is \"great\"");
        sm.Claim("claim \"mojo\" is \"good\"");
        Assert.IsTrue(sm.IsTrue("claim \"mojo\" is \"good\""));
        Assert.IsFalse(sm.IsTrue("claim \"mojo\" is \"great\""));
    }
}
