using NUnit.Framework;

using Syllogisms;

public class ParserTests {
    [Test]
    public void TestGetIndent() {
        Assert.AreEqual(0, Parser.GetIndentLevel("hello"));
        Assert.AreEqual(1, Parser.GetIndentLevel(" hello"));
        Assert.AreEqual(2, Parser.GetIndentLevel("  hello"));
    }

    [Test]
    public void TestParseLine() {
        Parser.Line line = Parser.ParseLine(":: is a (monster)");
        Assert.AreEqual(":: is a (monster)", line.text);
        Assert.AreEqual(0, line.indentLevel);
        Assert.AreEqual(Parser.LineType.Condition, line.type);
        Assert.AreEqual("is a %s", line.binding.key);
        Assert.AreEqual(0, line.children.Count);

        line = Parser.ParseLine("  + knows things");
        Assert.AreEqual("  + knows things", line.text);
        Assert.AreEqual(2, line.indentLevel);
        Assert.AreEqual(Parser.LineType.Claim, line.type);
        Assert.AreEqual("knows things", line.binding.key);
        Assert.AreEqual(0, line.children.Count);

        line = Parser.ParseLine("   * Action (thing)");
        Assert.AreEqual(Parser.LineType.Action, line.type);
        Assert.AreEqual("Action %s", line.binding.key);
    }

    [Test]
    public void TestParseFile() {
        Parser.Line[] lines = Parser.ParseFile(
            ":: someone is (something)\n" +
            "   :: child condition\n" +
            "       + child claim\n" +
            "\n" + 
            "   :: deindented condition\n" +
            "       + child claim 2\n" +
            "+ root claim\n"
        );
        Assert.AreEqual(2, lines.Length);
        Parser.Line line = lines[0];
        Assert.AreEqual(":: someone is (something)", line.text);
        Assert.AreEqual(0, line.indentLevel);
        Assert.AreEqual(2, line.children.Count);
        Parser.Line childCondition = line.children[0];
        Assert.AreEqual("   :: child condition", childCondition.text);
        Assert.AreEqual(1, childCondition.children.Count);
        Assert.AreEqual(line, childCondition.parent);
        Parser.Line childClaim = childCondition.children[0];
        Assert.AreEqual("       + child claim", childClaim.text);
        Assert.AreEqual(0, childClaim.children.Count);
        Assert.AreEqual(childCondition, childClaim.parent);
        Parser.Line deindentedCondition = line.children[1];
        Assert.AreEqual("   :: deindented condition", deindentedCondition.text);
        Assert.AreEqual(1, deindentedCondition.children.Count);
        Parser.Line rootClaim = lines[1];
        Assert.AreEqual("+ root claim", rootClaim.text);
        Assert.AreEqual(0, rootClaim.children.Count);
    }

    [Test]
    public void TestParseToken() {
        Parser.Token token = Parser.ParseToken("(name)");
        Assert.AreEqual(Parser.TokenType.Variable, token.type);
        Assert.AreEqual("name", token.value);

        token = Parser.ParseToken("\"string\"");
        Assert.AreEqual(Parser.TokenType.String, token.type);
        Assert.AreEqual("string", token.value);
    }

    [Test]
    public void TestGetBinding() {
        Parser.Binding binding = Parser.GetBinding("is (something)");
        Assert.AreEqual("is %s", binding.key);
        Parser.Token token = binding.tokens[0];
        Assert.AreEqual(Parser.TokenType.Variable, token.type);
        Assert.AreEqual("something", token.value);

        binding = Parser.GetBinding("(someone) is (something)");
        Parser.Token token1 = binding.tokens[0];
        Parser.Token token2 = binding.tokens[1];
        Assert.AreEqual(Parser.TokenType.Variable, token1.type);
        Assert.AreEqual("someone", token1.value);
        Assert.AreEqual(Parser.TokenType.Variable, token2.type);
        Assert.AreEqual("something", token2.value);

        binding = Parser.GetBinding("(someone) is \"a big meanie\"");
        token1 = binding.tokens[0];
        token2 = binding.tokens[1];
        Assert.AreEqual(Parser.TokenType.Variable, token1.type);
        Assert.AreEqual(Parser.TokenType.String, token2.type);
        Assert.AreEqual("someone", token1.value);
        Assert.AreEqual("a big meanie", token2.value);
    }
}
