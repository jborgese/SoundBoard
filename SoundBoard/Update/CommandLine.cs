using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoundBoard.Update
{
    /// <summary>
    /// Builds a Windows command line that <c>CommandLineToArgvW</c> (and therefore the CLR's <c>Main(string[] args)</c>)
    /// parses back into exactly the given arguments. .NET Framework has no <c>ProcessStartInfo.ArgumentList</c>, so the
    /// escaping has to be done by hand; the rules are the ones documented in
    /// https://learn.microsoft.com/en-us/cpp/c-language/parsing-c-command-line-arguments.
    /// </summary>
    internal static class CommandLine
    {
        private const char Backslash = '\\';
        private const char DoubleQuote = '"';

        /// <summary>
        /// Joins arguments into a single command line, quoting each one as needed.
        /// </summary>
        public static string Join(IEnumerable<string> args)
        {
            return string.Join(" ", args.Select(Quote));
        }

        /// <summary>
        /// Quotes a single argument. Arguments without whitespace or quotes are returned unchanged; everything else is
        /// wrapped in double quotes with embedded quotes and the backslashes that precede them escaped.
        /// </summary>
        public static string Quote(string arg)
        {
            if (arg == null)
            {
                arg = string.Empty;
            }

            if (arg.Length > 0 && arg.All(c => !char.IsWhiteSpace(c) && c != DoubleQuote))
            {
                return arg;
            }

            StringBuilder sb = new StringBuilder(arg.Length + 2);
            sb.Append(DoubleQuote);

            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == Backslash)
                {
                    backslashes++;
                    continue;
                }

                if (c == DoubleQuote)
                {
                    // Backslashes before a quote must be doubled, then the quote itself is escaped.
                    sb.Append(Backslash, backslashes * 2 + 1);
                    sb.Append(DoubleQuote);
                }
                else
                {
                    sb.Append(Backslash, backslashes);
                    sb.Append(c);
                }

                backslashes = 0;
            }

            // Backslashes before the closing quote must be doubled so they are not taken as escaping it.
            sb.Append(Backslash, backslashes * 2);
            sb.Append(DoubleQuote);
            return sb.ToString();
        }
    }
}
