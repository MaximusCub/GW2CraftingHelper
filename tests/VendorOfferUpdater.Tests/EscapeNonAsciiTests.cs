using VendorOfferUpdater;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    // EscapeNonAscii restores
    // ref/vendor_offers.json's established escaping convention (non-ASCII
    // escaped as lowercase \uXXXX, every ASCII character - including
    // apostrophe/ampersand - left literal) on top of
    // JavaScriptEncoder.UnsafeRelaxedJsonEscaping's own output.
    public class EscapeNonAsciiTests
    {
        [Fact]
        public void AsciiApostrophe_LeftLiteral()
        {
            string result = Program.EscapeNonAscii("\"Hearth's Glow\"");

            Assert.Equal("\"Hearth's Glow\"", result);
        }

        [Fact]
        public void AsciiAmpersandAngleBrackets_LeftLiteral()
        {
            string result = Program.EscapeNonAscii("\"A & B < C > D\"");

            Assert.Equal("\"A & B < C > D\"", result);
        }

        [Fact]
        public void EmDash_EscapedAsLowercaseUnicode()
        {
            // Source stays ASCII-only (repo invariant): the U+2014 em
            // dash below is written as a \u2014 escape sequence, which
            // the C# compiler resolves to the identical runtime
            // character a pasted literal would be.
            string result = Program.EscapeNonAscii("\"Homestead Refinement\u2014Farm\"");

            Assert.Equal("\"Homestead Refinement\\u2014Farm\"", result);
        }

        [Fact]
        public void AccentedLatin_EscapedAsLowercaseUnicode()
        {
            string result = Program.EscapeNonAscii("\"caf\u00e9\"");

            Assert.Equal("\"caf\\u00e9\"", result);
        }

        [Fact]
        public void AlreadyEscapedJsonQuoteAndBackslash_Unaffected()
        {
            // Input here already went through JsonSerializer's own escaping
            // (a literal quote became \", a literal backslash became \\) -
            // EscapeNonAscii must not double-escape or otherwise disturb
            // those ASCII escape sequences.
            string alreadyEscaped = "\"a\\\"b\\\\c\"";

            string result = Program.EscapeNonAscii(alreadyEscaped);

            Assert.Equal(alreadyEscaped, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void NullOrEmpty_ReturnsSameValue(string input)
        {
            Assert.Equal(input, Program.EscapeNonAscii(input));
        }

        [Fact]
        public void PlainAsciiOnly_Unchanged()
        {
            string plain = "{\"offerId\":\"abc123\",\"outputItemId\":1}";

            Assert.Equal(plain, Program.EscapeNonAscii(plain));
        }
    }
}
