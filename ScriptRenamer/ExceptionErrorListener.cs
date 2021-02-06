using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace ScriptRenamer
{
    public class ExceptionErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        public static ExceptionErrorListener Instance { get; } = new ExceptionErrorListener();

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new ParseCanceledException($"Line {line} Column {charPositionInLine}: {msg}", e);
        }

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            SyntaxError(output, recognizer, 0, line, charPositionInLine, msg, e);
        }
    }
}
