/*
    Gramática BNF original:

        Expr ::= Expr "+" Term
        Expr ::= Term
        Term ::= Term "*" PowTerm
        Term ::= PowTerm
        PowTerm ::= Fact "^" PowTerm
        PowTerm ::= Fact
        Fact ::= "int"
        Fact ::= "(" Expr ")"

    Gramática LL(1):

        (0) Prog ::= Expr "EOF"
        (1) Expr ::= Term ("+" Term)*
        (2) Term ::= PowTerm ("*" PowTerm)*
        (3) PowTerm ::= Fact ("^" PowTerm)?
        (4) Fact ::= "int" | "(" Expr ")"
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public enum TokenCategory {
    INT, PLUS, TIMES, POW, OPEN_PAR, CLOSE_PAR, EOF, BAD_TOKEN
}

public class Token {
    public TokenCategory Category { get; }
    public String Lexeme { get; }

    public Token(TokenCategory category, String lexeme) {
        Category = category;
        Lexeme = lexeme;
    }

    public override String ToString() {
        return $"[{Category}, \"{Lexeme}\"]";
    }
}

public class Scanner {
    readonly String input;
    static readonly Regex regex = new Regex(
        @"(\d+)|([+])|([*])|(\^)|([(])|([)])|(\s)|(.)");

    public Scanner(String input) {
        this.input = input;
    }

    public IEnumerable<Token> Scan() {
        var result = new LinkedList<Token>();

        foreach (Match m in regex.Matches(input)) {
            if (m.Groups[1].Success) {
                result.AddLast(new Token(TokenCategory.INT, m.Value));
            } else if (m.Groups[2].Success) {
                result.AddLast(new Token(TokenCategory.PLUS, m.Value));
            } else if (m.Groups[3].Success) {
                result.AddLast(new Token(TokenCategory.TIMES, m.Value));
            } else if (m.Groups[4].Success) {
                result.AddLast(new Token(TokenCategory.POW, m.Value));
            } else if (m.Groups[5].Success) {
                result.AddLast(new Token(TokenCategory.OPEN_PAR, m.Value));
            } else if (m.Groups[6].Success) {
                result.AddLast(new Token(TokenCategory.CLOSE_PAR, m.Value));
            } else if (m.Groups[7].Success) {
                // skip
            } else if (m.Groups[8].Success) {
                result.AddLast(new Token(TokenCategory.BAD_TOKEN, m.Value));
            }
        }
        result.AddLast(new Token(TokenCategory.EOF, null));

        return result;
    }
}

public class SyntaxError: Exception {}

public class Parser {
    IEnumerator<Token> tokenStream;

    public Parser(IEnumerator<Token> tokenStream) {
        this.tokenStream = tokenStream;
        this.tokenStream.MoveNext();
    }

    public TokenCategory Current {
        get {
            return tokenStream.Current.Category;
        }
    }

    public Token Expect(TokenCategory category) {
        if (Current == category) {
            Token current = tokenStream.Current;
            tokenStream.MoveNext();
            return current;
        } else {
            throw new SyntaxError();
        }
    }

    // (0)
    public Node Prog() {
        var node = new Prog();
        node.Add(Expr());
        Expect(TokenCategory.EOF);
        return node;
    }

    // (1)
    public Node Expr() {
        var result = Term();
        while (Current == TokenCategory.PLUS) {
            var token = Expect(TokenCategory.PLUS);
            var node = new Plus();
            node.AnchorToken = token;
            node.Add(result);
            node.Add(Term());
            result = node;
        }
        return result;
    }

    // (2)
    public Node Term() {
        var result = PowTerm();
        while (Current == TokenCategory.TIMES) {
            var token = Expect(TokenCategory.TIMES);
            var node = new Times();
            node.AnchorToken = token;
            node.Add(result);
            node.Add(PowTerm());
            result = node;
        }
        return result;
    }

    // (3)
    public Node PowTerm() {
        var result = Fact();
        if (Current == TokenCategory.POW) {
            var token = Expect(TokenCategory.POW);
            var node = new Pow();
            node.AnchorToken = token;
            node.Add(result);
            node.Add(PowTerm());
            result = node;
        }
        return result;
    }

    // (4)
    public Node Fact() {
        switch (Current) {
            case TokenCategory.INT:
            {
                var token = Expect(TokenCategory.INT);
                var result = new Int();
                result.AnchorToken = token;
                return result;
            }
            case TokenCategory.OPEN_PAR:
            {
                Expect(TokenCategory.OPEN_PAR);
                var result = Expr();
                Expect(TokenCategory.CLOSE_PAR);
                return result;
            }
            default:
                throw new SyntaxError();
        }
    }
}

public class Node: IEnumerable<Node> {

    IList<Node> children = new List<Node>();

    public Node this[int index] {
        get {
            return children[index];
        }
    }

    public Token AnchorToken { get; set; }

    public void Add(Node node) {
        children.Add(node);
    }

    public IEnumerator<Node> GetEnumerator() {
        return children.GetEnumerator();
    }

    System.Collections.IEnumerator
    System.Collections.IEnumerable.GetEnumerator() {
        throw new NotImplementedException();
    }

    public override string ToString() {
        return $"{GetType().Name} {AnchorToken}";
    }

    public string ToStringTree() {
        var sb = new StringBuilder();
        TreeTraversal(this, "", sb);
        return sb.ToString();
    }

    static void TreeTraversal(Node node, string indent, StringBuilder sb) {
        sb.Append(indent);
        sb.Append(node);
        sb.Append('\n');
        foreach (var child in node.children) {
            TreeTraversal(child, indent + "  ", sb);
        }
    }
}

public class Prog: Node {}
public class Plus: Node {}
public class Times: Node {}
public class Pow: Node {}
public class Int: Node {}

public class EvalVisitor {
    public int Visit(Prog node) {
        return Visit((dynamic) node[0]);
    }

    public int Visit(Plus node) {
        return Visit((dynamic) node[0])
            + Visit((dynamic) node[1]);
    }

    public int Visit(Times node) {
        return Visit((dynamic) node[0])
            * Visit((dynamic) node[1]);
    }

    public int Visit(Pow node) {
        return (int) Math.Pow(Visit((dynamic) node[0]),
                              Visit((dynamic) node[1]));

        ;
    }

    public int Visit(Int node) {
        return Int32.Parse(node.AnchorToken.Lexeme);
    }
}

public class SemanticError: Exception {}

public class SemanticVisitor {

    public void Visit(Prog node) {
        Visit((dynamic) node[0]);
    }

    public void Visit(Plus node) {
        Visit((dynamic) node[0]);
        Visit((dynamic) node[1]);
    }

    public void Visit(Times node) {
        Visit((dynamic) node[0]);
        Visit((dynamic) node[1]);
    }

    public void Visit(Pow node) {
        Visit((dynamic) node[0]);
        Visit((dynamic) node[1]);
    }

    public void Visit(Int node) {
        int result;
        if (!Int32.TryParse(node.AnchorToken.Lexeme, out result)) {
            throw new SemanticError();
        }
    }

}

public class WATVisitor {

    public String Visit(Prog node) {
        return
          "(module\n"
          + "  (import \"math\" \"pow\" (func $pow (param i32 i32) (result i32)))\n"
          + "  (func\n"
          + "    (export \"start\")\n"
          + "    (result i32)\n"
          + Visit((dynamic) node[0])
          + "  )\n"
          + ")\n";
    }

    public String Visit(Plus node) {
        return Visit((dynamic) node[0])
            + Visit((dynamic) node[1])
            + "    i32.add\n";
    }

    public String Visit(Times node) {
        return Visit((dynamic) node[0])
            + Visit((dynamic) node[1])
            + "    i32.mul\n";
    }

    public String Visit(Pow node) {
        return Visit((dynamic) node[0])
            + Visit((dynamic) node[1])
            + "    call $pow\n";
    }

    public String Visit(Int node) {
        return $"    i32.const {node.AnchorToken.Lexeme}\n";
    }
}

public class Driver {
    public static void Main() {
        Console.Write("> ");
        var line = Console.ReadLine();
        var parser = new Parser(new Scanner(line).Scan().GetEnumerator());
        try {
            var ast = parser.Prog();
            // Console.WriteLine(result.ToStringTree());
            new SemanticVisitor().Visit((dynamic) ast);
            // Console.WriteLine(new EvalVisitor().Visit((dynamic) ast));
            File.WriteAllText("output.wat", new WATVisitor().Visit((dynamic) ast));
        } catch (SyntaxError) {
            Console.WriteLine("Bad syntax!");
        } catch (SemanticError) {
            Console.WriteLine("Bad semantics!");
        }
    }
}
